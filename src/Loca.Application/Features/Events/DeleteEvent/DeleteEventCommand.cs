using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.DeleteEvent;

public sealed record DeleteEventCommand(Guid EventId) : IRequest<Result>;

internal sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<DeleteEventCommandHandler> logger)
    : IRequestHandler<DeleteEventCommand, Result>
{
    /// <summary>
    /// Silinebilir durumlar.
    /// </summary>
    /// <remarks>
    /// Yayindaki bir etkinlik SILINMEZ, iptal edilir: silme kullaniciya
    /// hicbir sey soylemez, iptal ise bilet sahiplerine bildirim ve iade
    /// sureci baslatir (Gun 7). Etkinligin listeden sessizce kaybolmasi
    /// bilet almis kisiyi ortada birakirdi.
    /// </remarks>
    private static readonly EventStatus[] DeletableStatuses =
    [
        EventStatus.Draft,
        EventStatus.PendingApproval,
        EventStatus.Cancelled
    ];

    public async Task<Result> Handle(DeleteEventCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        if (!DeletableStatuses.Contains(ev.Status))
        {
            return Result.Failure(Error.Conflict(
                "Event.NotDeletable",
                $"Bu durumdaki etkinlik silinemez: {ev.Status}. " +
                "Yayindaki etkinlik silinmez, iptal edilir."));
        }

        // Durum kontrolu yetmez: iptal edilmis bir etkinligin satilmis
        // biletleri iade surecinde olabilir ve kayit ortadan kalkamaz.
        if (await events.HasCommittedSeatsAsync(ev.Id, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "Event.HasCommittedSeats",
                "Satilmis veya rezerve koltugu olan etkinlik silinemez."));
        }

        // Fiziksel silme degil: interceptor bu istegi isaretlemeye cevirir.
        events.Remove(ev);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Etkinlik silindi (soft delete). EtkinlikId: {EtkinlikId}", ev.Id);

        return Result.Success();
    }
}
