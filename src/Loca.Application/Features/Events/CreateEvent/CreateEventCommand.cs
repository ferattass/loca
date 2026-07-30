using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.CreateEvent;

/// <remarks>
/// Organizator kimligi istek govdesinde YOK: token'dan okunuyor. Govdeden
/// alinsaydi bir organizator baskasinin adina etkinlik olusturabilirdi.
/// </remarks>
public sealed record CreateEventCommand(
    Guid CategoryId,
    string Title,
    string Description,
    string CancellationPolicy,
    Guid CityId,
    Guid VenueId,
    Guid HallId,
    DateTime EventDateUtc,
    int DurationMinutes,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    int? MinimumAge) : IRequest<Result<Guid>>;

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
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

        RuleFor(command => command.CityId).NotEmpty().WithMessage("Sehir zorunludur.");
        RuleFor(command => command.VenueId).NotEmpty().WithMessage("Mekan zorunludur.");
        RuleFor(command => command.HallId).NotEmpty().WithMessage("Salon zorunludur.");

        RuleFor(command => command.DurationMinutes)
            .GreaterThan(0).WithMessage("Sure sifirdan buyuk olmalidir.")
            .LessThanOrEqualTo(EventSchedule.MaxDurationMinutes)
            .WithMessage($"Sure en fazla {EventSchedule.MaxDurationMinutes} dakika olabilir.");

        // Tarih siralamasi kurallari domain'de (EventSchedule) da var.
        // Burada da olmasi tekrar degil: FluentValidation ALAN BAZLI hata
        // listesi dondurup 400 verir, domain ise tek mesajla 409. Formda
        // hangi alanin yanlis oldugunu gostermek icin ilkine ihtiyac var.
        RuleFor(command => command.SalesEndsAtUtc)
            .GreaterThan(command => command.SalesStartsAtUtc)
            .WithMessage("Satis bitisi satis baslangicindan sonra olmalidir.");

        RuleFor(command => command.SalesEndsAtUtc)
            .LessThanOrEqualTo(command => command.EventDateUtc)
            .WithMessage("Bilet satisi etkinlik baslamadan once bitmelidir.");

        RuleFor(command => command.MinimumAge)
            .InclusiveBetween(0, 99).WithMessage("Yas siniri 0 ile 99 arasinda olmalidir.")
            .When(command => command.MinimumAge is not null);
    }
}

internal sealed class CreateEventCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<CreateEventCommandHandler> logger)
    : IRequestHandler<CreateEventCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateEventCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } organizerId)
            return Result.Failure<Guid>(EventErrors.Unauthenticated);

        if (!await events.CategoryExistsAsync(request.CategoryId, cancellationToken))
            return Result.Failure<Guid>(EventErrors.CategoryNotFound);

        // Uc kimlik tek tek var olabilir ama birbirine ait olmayabilir.
        if (!await events.PlaceIsValidAsync(
                request.CityId, request.VenueId, request.HallId, cancellationToken))
        {
            return Result.Failure<Guid>(EventErrors.PlaceInvalid);
        }

        var place = new EventPlace(request.CityId, request.VenueId, request.HallId);

        var schedule = new EventSchedule(
            request.EventDateUtc,
            request.DurationMinutes,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc);

        var ev = new Event(
            organizerId,
            request.CategoryId,
            request.Title,
            request.Description,
            place,
            schedule,
            request.CancellationPolicy,
            request.MinimumAge);

        events.Add(ev);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Etkinlik olusturuldu. EtkinlikId: {EtkinlikId}, OrganizatorId: {OrganizatorId}",
            ev.Id,
            organizerId);

        return Result.Success(ev.Id);
    }
}
