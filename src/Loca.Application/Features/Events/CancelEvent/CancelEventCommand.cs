using System.Text.Json;
using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.CancelEvent;

public sealed record CancelEventCommand(Guid EventId, string Reason) : IRequest<Result>;

public sealed class CancelEventCommandValidator : AbstractValidator<CancelEventCommand>
{
    public CancelEventCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("Iptal gerekcesi zorunludur.")
            .MaximumLength(500).WithMessage("Iptal gerekcesi en fazla 500 karakter olabilir.");
    }
}

internal sealed class CancelEventCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<CancelEventCommandHandler> logger)
    : IRequestHandler<CancelEventCommand, Result>
{
    public async Task<Result> Handle(CancelEventCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Oturumlar da iptal edilecegi icin aggregate gerekli.
        var ev = await events.GetAggregateAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        var oncekiDurum = ev.Status;

        // Durum gecisi ve oturumlarin iptali domain'de, tek cagriyla.
        ev.Cancel(clock.UtcNow, request.Reason);

        // Iptal, bilet almis kullanicilari etkileyen bir karar; kim ne zaman
        // hangi gerekceyle iptal etti sorusunun cevabi denetim kaydinda kalir.
        events.AddAuditLog(new AuditLog(
            nameof(Event),
            ev.Id,
            AuditActions.Cancelled,
            clock.UtcNow,
            oldValues: JsonSerializer.Serialize(new { Status = oncekiDurum.ToString() }),
            newValues: JsonSerializer.Serialize(new
            {
                Status = ev.Status.ToString(),
                Reason = request.Reason
            }),
            userId: currentUser.UserId));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Biletlerin iadesi ve bilet sahiplerine bildirim Gun 7'de Outbox
        // uzerinden yapilacak; bugun yalnizca durum degisiyor.
        logger.LogWarning(
            "Etkinlik iptal edildi. EtkinlikId: {EtkinlikId}, OncekiDurum: {OncekiDurum}",
            ev.Id,
            oncekiDurum);

        return Result.Success();
    }
}
