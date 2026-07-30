using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.CreateEventSession;

public sealed record CreateEventSessionCommand(
    Guid EventId,
    Guid HallId,
    Guid SeatLayoutId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc) : IRequest<Result<Guid>>;

public sealed class CreateEventSessionCommandValidator : AbstractValidator<CreateEventSessionCommand>
{
    public CreateEventSessionCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.HallId).NotEmpty().WithMessage("Salon zorunludur.");
        RuleFor(command => command.SeatLayoutId).NotEmpty().WithMessage("Oturma plani zorunludur.");

        RuleFor(command => command.EndsAtUtc)
            .GreaterThan(command => command.StartsAtUtc)
            .WithMessage("Oturum bitisi baslangicindan sonra olmalidir.");

        RuleFor(command => command.SalesEndsAtUtc)
            .GreaterThan(command => command.SalesStartsAtUtc)
            .WithMessage("Satis bitisi satis baslangicindan sonra olmalidir.");

        RuleFor(command => command.SalesEndsAtUtc)
            .LessThanOrEqualTo(command => command.StartsAtUtc)
            .WithMessage("Satis, oturum baslamadan once bitmelidir.");
    }
}

internal sealed class CreateEventSessionCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<CreateEventSessionCommandHandler> logger)
    : IRequestHandler<CreateEventSessionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateEventSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Aggregate yukleniyor cunku Event.AddSession, ayni etkinligin diger
        // oturumlariyla cakismayi kendisi kontrol ediyor.
        var ev = await events.GetAggregateAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure<Guid>(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure<Guid>(hata);

        if (!await events.SeatLayoutBelongsToHallAsync(
                request.SeatLayoutId, request.HallId, cancellationToken))
        {
            return Result.Failure<Guid>(EventErrors.SeatLayoutNotInHall);
        }

        // FARKLI ETKINLIKLERIN AYNI SALONDAKI CAKISMASI.
        // Ayni etkinligin oturumlari arasindaki cakisma Event.AddSession
        // icinde, bellekte kontrol ediliyor; bu sorgu ise aggregate'in
        // gormedigi satirlara bakiyor. Sartnamenin "aynı salon aynı zaman
        // aralığında iki etkinliğe atanamaz" kuralinin kalan yarisi burada.
        var cakisma = await events.HallHasSessionConflictAsync(
            request.HallId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            ev.Id,
            cancellationToken);

        if (cakisma)
            return Result.Failure<Guid>(EventErrors.HallConflict);

        var session = ev.AddSession(
            request.HallId,
            request.SeatLayoutId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc);

        // Bilet turundeki ile ayni gerekce: anahtar dolu geldigi icin EF bu
        // yeni oturumu kendiliginden Modified sayar ve UPDATE uretir.
        events.AddSession(session);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Oturum eklendi. EtkinlikId: {EtkinlikId}, OturumId: {OturumId}, Baslangic: {Baslangic}",
            ev.Id,
            session.Id,
            request.StartsAtUtc);

        return Result.Success(session.Id);
    }
}
