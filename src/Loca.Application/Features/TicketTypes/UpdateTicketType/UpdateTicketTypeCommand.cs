using System.Text.Json;
using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.TicketTypes.UpdateTicketType;

public sealed record UpdateTicketTypeCommand(
    Guid TicketTypeId,
    string Name,
    decimal Price,
    string Currency,
    int Quota,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc) : IRequest<Result>;

public sealed class UpdateTicketTypeCommandValidator : AbstractValidator<UpdateTicketTypeCommand>
{
    public UpdateTicketTypeCommandValidator()
    {
        RuleFor(command => command.TicketTypeId).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Bilet turu adi zorunludur.")
            .MaximumLength(100).WithMessage("Bilet turu adi en fazla 100 karakter olabilir.");

        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Fiyat negatif olamaz.");

        RuleFor(command => command.Currency)
            .NotEmpty().WithMessage("Para birimi zorunludur.")
            .Length(3).WithMessage("Para birimi 3 harfli ISO kodu olmalidir.");

        RuleFor(command => command.Quota)
            .GreaterThan(0).WithMessage("Kontenjan sifirdan buyuk olmalidir.");

        RuleFor(command => command.SalesEndsAtUtc)
            .GreaterThan(command => command.SalesStartsAtUtc)
            .WithMessage("Satis bitisi satis baslangicindan sonra olmalidir.");
    }
}

internal sealed class UpdateTicketTypeCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<UpdateTicketTypeCommandHandler> logger)
    : IRequestHandler<UpdateTicketTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTicketTypeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticketType = await events.GetTicketTypeAsync(request.TicketTypeId, cancellationToken);

        if (ticketType is null)
            return Result.Failure(EventErrors.TicketTypeNotFound);

        var ev = await events.GetByIdAsync(ticketType.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        // Kontenjan kontrolu: turun KENDI eski kontenjani toplamdan
        // cikariliyor, aksi hâlde ayni kontenjan iki kez sayilir ve
        // degistirilmeyen bir tur bile kapasiteyi asmis gorunurdu.
        var kapasite = await events.HallCapacityAsync(ev.HallId, cancellationToken);
        var digerleri = await events.TotalQuotaAsync(ev.Id, ticketType.Id, cancellationToken);

        if (digerleri + request.Quota > kapasite)
            return Result.Failure(EventErrors.QuotaExceedsHallCapacity);

        var eskiFiyat = ticketType.Price;
        var yeniFiyat = new Money(request.Price, request.Currency);

        ticketType.Update(
            request.Name, request.Quota, request.SalesStartsAtUtc, request.SalesEndsAtUtc);

        if (yeniFiyat != eskiFiyat)
        {
            // ChangePrice eski degeri doner; denetim kaydi "eski → yeni"
            // ikilisini gerektiriyor ve bu bilgi baska hicbir yerde tutulmuyor.
            var oncekiDeger = ticketType.ChangePrice(yeniFiyat);

            // SARTNAME KURALI: satisi baslamis bilet turunun fiyat
            // degisikligi LOGLANMALIDIR. Satis baslamamissa denetim kaydi
            // yazilmiyor — organizator taslak asamasinda fiyati defalarca
            // degistirebilir ve her denemenin kaydi denetim tablosunu
            // gurultuye bogar.
            if (ticketType.IsOnSale(clock.UtcNow))
            {
                events.AddAuditLog(new AuditLog(
                    nameof(TicketType),
                    ticketType.Id,
                    AuditActions.PriceChanged,
                    clock.UtcNow,
                    oldValues: JsonSerializer.Serialize(new
                    {
                        Amount = oncekiDeger.Amount,
                        oncekiDeger.Currency
                    }),
                    newValues: JsonSerializer.Serialize(new
                    {
                        yeniFiyat.Amount,
                        yeniFiyat.Currency
                    }),
                    userId: currentUser.UserId));

                logger.LogWarning(
                    "Satisi baslamis bilet turunun fiyati degisti. BiletTuruId: {BiletTuruId}, " +
                    "Eski: {Eski}, Yeni: {Yeni}",
                    ticketType.Id,
                    oncekiDeger,
                    yeniFiyat);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Uretilmis EventSeats'lerin fiyati DEGISMEZ: fiyat uretim aninda
        // kopyalanmisti. Yeni fiyat yalnizca bundan sonra uretilecek
        // koltuklar icin gecerli.
        logger.LogInformation("Bilet turu guncellendi. BiletTuruId: {BiletTuruId}", ticketType.Id);

        return Result.Success();
    }
}
