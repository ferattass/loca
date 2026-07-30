using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.TicketTypes.AssignTicketTypeSection;

/// <param name="SeatSectionId">
/// <c>null</c> gecilirse tur bolum atamasindan cikar ve varsayilan tur olur.
/// </param>
public sealed record AssignTicketTypeSectionCommand(Guid TicketTypeId, Guid? SeatSectionId)
    : IRequest<Result>;

internal sealed class AssignTicketTypeSectionCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<AssignTicketTypeSectionCommandHandler> logger)
    : IRequestHandler<AssignTicketTypeSectionCommand, Result>
{
    public async Task<Result> Handle(
        AssignTicketTypeSectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticketType = await events.GetTicketTypeAsync(request.TicketTypeId, cancellationToken);

        if (ticketType is null)
            return Result.Failure(EventErrors.TicketTypeNotFound);

        // Aggregate: "ayni bolum iki aktif ture atanamaz" kurali diger
        // turleri gormeyi gerektiriyor.
        var ev = await events.GetAggregateAsync(ticketType.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        if (request.SeatSectionId is { } sectionId &&
            !await events.SectionBelongsToHallAsync(sectionId, ev.HallId, cancellationToken))
        {
            return Result.Failure(EventErrors.SectionNotInHall);
        }

        // Koltuklar uretildikten sonra atama degistirilirse uretilmis
        // EventSeats eski ture bagli kalir; fiyat kopyalandigi icin satilmis
        // biletler etkilenmez ama plan ile fiyat tablosu ayrisir.
        if (await events.HasCommittedSeatsAsync(ev.Id, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "TicketType.EventHasCommittedSeats",
                "Satisi baslamis etkinlikte bilet turunun bolum atamasi degistirilemez."));
        }

        // Kural aggregate'te: ayni bolum baska bir aktif ture atanmissa
        // DomainException → 409.
        ev.AssignTicketTypeToSection(ticketType.Id, request.SeatSectionId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bilet turu bolume atandi. BiletTuruId: {BiletTuruId}, BolumId: {BolumId}",
            ticketType.Id,
            request.SeatSectionId);

        return Result.Success();
    }
}
