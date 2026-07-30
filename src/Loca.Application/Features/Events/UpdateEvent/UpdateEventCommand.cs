using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.UpdateEvent;

/// <remarks>
/// Tarih ve yer alanlari opsiyonel: yayina alinmis etkinlikte
/// gonderilmedikleri surece istek gecer. Gonderildiklerinde domain
/// "yayindan sonra kritik alan degistirilemez" diyerek 409 doner.
/// </remarks>
public sealed record UpdateEventCommand(
    Guid EventId,
    Guid CategoryId,
    string Title,
    string Description,
    string CancellationPolicy,
    int? MinimumAge,
    Guid? CityId,
    Guid? VenueId,
    Guid? HallId,
    DateTime? EventDateUtc,
    int? DurationMinutes,
    DateTime? SalesStartsAtUtc,
    DateTime? SalesEndsAtUtc) : IRequest<Result>;

public sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();

        RuleFor(command => command.CategoryId)
            .NotEmpty().WithMessage("Kategori zorunludur.");

        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Etkinlik basligi zorunludur.")
            .MaximumLength(250).WithMessage("Baslik en fazla 250 karakter olabilir.");

        RuleFor(command => command.Description)
            .NotEmpty().WithMessage("Aciklama zorunludur.")
            .MaximumLength(10_000).WithMessage("Aciklama en fazla 10000 karakter olabilir.");

        RuleFor(command => command.CancellationPolicy)
            .NotEmpty().WithMessage("Iptal ve iade politikasi zorunludur.")
            .MaximumLength(1000).WithMessage("Iptal politikasi en fazla 1000 karakter olabilir.");

        RuleFor(command => command.MinimumAge)
            .InclusiveBetween(0, 99).WithMessage("Yas siniri 0 ile 99 arasinda olmalidir.")
            .When(command => command.MinimumAge is not null);

        RuleFor(command => command.DurationMinutes)
            .GreaterThan(0).WithMessage("Sure sifirdan buyuk olmalidir.")
            .LessThanOrEqualTo(EventSchedule.MaxDurationMinutes)
            .WithMessage($"Sure en fazla {EventSchedule.MaxDurationMinutes} dakika olabilir.")
            .When(command => command.DurationMinutes is not null);

        // Yer ve tarih alanlari "hep birlikte veya hic": ucunden ikisi
        // gonderilirse hangi degerin korunacagi belirsiz kalir.
        RuleFor(command => command)
            .Must(HasCompletePlace)
            .WithMessage("Yer degisikliginde sehir, mekan ve salon birlikte gonderilmelidir.")
            .WithName(nameof(UpdateEventCommand.VenueId));

        RuleFor(command => command)
            .Must(HasCompleteSchedule)
            .WithMessage(
                "Tarih degisikliginde etkinlik tarihi, sure ve satis tarihleri birlikte gonderilmelidir.")
            .WithName(nameof(UpdateEventCommand.EventDateUtc));
    }

    private static bool HasCompletePlace(UpdateEventCommand command)
    {
        var verilen = new[] { command.CityId, command.VenueId, command.HallId }
            .Count(deger => deger is not null);

        return verilen is 0 or 3;
    }

    private static bool HasCompleteSchedule(UpdateEventCommand command)
    {
        var verilen = new object?[]
        {
            command.EventDateUtc,
            command.DurationMinutes,
            command.SalesStartsAtUtc,
            command.SalesEndsAtUtc
        }.Count(deger => deger is not null);

        return verilen is 0 or 4;
    }
}

internal sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<UpdateEventCommandHandler> logger)
    : IRequestHandler<UpdateEventCommand, Result>
{
    public async Task<Result> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        if (request.CategoryId != ev.CategoryId &&
            !await events.CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return Result.Failure(EventErrors.CategoryNotFound);
        }

        // Kritik olmayan alanlar. Gecersiz durum (iptal/tamamlanmis) domain
        // tarafinda DomainException'a cevrilir ve 409 doner.
        ev.UpdateDetails(
            request.Title,
            request.Description,
            request.CategoryId,
            request.CancellationPolicy,
            request.MinimumAge);

        if (request.CityId is { } cityId &&
            request.VenueId is { } venueId &&
            request.HallId is { } hallId)
        {
            if (!await events.PlaceIsValidAsync(cityId, venueId, hallId, cancellationToken))
                return Result.Failure(EventErrors.PlaceInvalid);

            ev.ChangePlace(new EventPlace(cityId, venueId, hallId));
        }

        if (request.EventDateUtc is { } eventDate &&
            request.DurationMinutes is { } duration &&
            request.SalesStartsAtUtc is { } salesStart &&
            request.SalesEndsAtUtc is { } salesEnd)
        {
            ev.Reschedule(new EventSchedule(eventDate, duration, salesStart, salesEnd));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Etkinlik guncellendi. EtkinlikId: {EtkinlikId}", ev.Id);

        return Result.Success();
    }
}
