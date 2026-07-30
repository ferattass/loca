using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.TicketTypes.CreateTicketType;

/// <param name="RequiresVerification">
/// Ogrenci bileti gibi belge dogrulamasi gerektiren turler icin.
/// Dogrulamanin kendisi <c>StudentVerifications</c> tablosunda tutulur.
/// </param>
/// <param name="SeatSectionId">
/// Verilirse tur yalnizca o bolumun koltuklarini fiyatlandirir. Bos
/// birakilirsa eslesmeyen tum bolumler icin varsayilan tur olur.
/// </param>
public sealed record CreateTicketTypeCommand(
    Guid EventId,
    string Name,
    decimal Price,
    string Currency,
    int Quota,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    bool RequiresVerification,
    Guid? SeatSectionId) : IRequest<Result<Guid>>;

public sealed class CreateTicketTypeCommandValidator : AbstractValidator<CreateTicketTypeCommand>
{
    public CreateTicketTypeCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Bilet turu adi zorunludur.")
            .MaximumLength(100).WithMessage("Bilet turu adi en fazla 100 karakter olabilir.");

        // Sartname: "Fiyat sifirdan kucuk olamaz." Sifir serbest — davetiye
        // veya basin bileti gibi ucretsiz turler gercek bir ihtiyac.
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

internal sealed class CreateTicketTypeCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<CreateTicketTypeCommandHandler> logger)
    : IRequestHandler<CreateTicketTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateTicketTypeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ad tekilligi ve bolum cakismasi kontrolleri diger bilet turlerini
        // gormeyi gerektiriyor; aggregate yuklenmeli.
        var ev = await events.GetAggregateAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure<Guid>(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure<Guid>(hata);

        if (request.SeatSectionId is { } sectionId)
        {
            // Bolum baska bir salonun planina aitse koltuk uretiminde hicbir
            // koltukla eslesmez ve tur sessizce isesiz kalirdi.
            if (!await events.SectionBelongsToHallAsync(sectionId, ev.HallId, cancellationToken))
                return Result.Failure<Guid>(EventErrors.SectionNotInHall);
        }

        // Sartname: "Kontenjan salon kapasitesini asamaz."
        var kapasite = await events.HallCapacityAsync(ev.HallId, cancellationToken);
        var mevcutKontenjan = await events.TotalQuotaAsync(ev.Id, null, cancellationToken);

        if (mevcutKontenjan + request.Quota > kapasite)
            return Result.Failure<Guid>(EventErrors.QuotaExceedsHallCapacity);

        var ticketType = ev.AddTicketType(
            request.Name,
            new Money(request.Price, request.Currency),
            request.Quota,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc,
            request.RequiresVerification,
            request.SeatSectionId);

        // Aggregate nesneyi kurdu ve koleksiyona ekledi, ama EF'e "bu YENI"
        // demek ayri bir is: anahtar zaten dolu oldugu icin EF kendiliginden
        // Modified isaretliyor ve INSERT yerine UPDATE uretiyor.
        events.AddTicketType(ticketType);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bilet turu eklendi. EtkinlikId: {EtkinlikId}, BiletTuruId: {BiletTuruId}, Ad: {Ad}",
            ev.Id,
            ticketType.Id,
            ticketType.Name);

        return Result.Success(ticketType.Id);
    }
}
