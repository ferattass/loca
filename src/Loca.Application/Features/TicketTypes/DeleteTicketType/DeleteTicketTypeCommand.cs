using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.TicketTypes.DeleteTicketType;

public sealed record DeleteTicketTypeCommand(Guid TicketTypeId) : IRequest<Result>;

internal sealed class DeleteTicketTypeCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<DeleteTicketTypeCommandHandler> logger)
    : IRequestHandler<DeleteTicketTypeCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTicketTypeCommand request, CancellationToken cancellationToken)
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

        if (await events.TicketTypeHasCommittedSeatsAsync(ticketType.Id, cancellationToken))
            return Result.Failure(EventErrors.TicketTypeHasSoldSeats);

        // Uretilmis ama satilmamis koltuklar bu ture yabanci anahtarla bagli
        // (Restrict). Fiziksel silme onlari yetim birakacagi icin tur
        // pasifleştiriliyor: satista gorunmez, gecmis kayitlar bozulmaz.
        // Ayni yaklasim Gun 4'te kullanilmis oturma plani icin de secilmisti.
        ticketType.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bilet turu pasifleştirildi. BiletTuruId: {BiletTuruId}", ticketType.Id);

        return Result.Success();
    }
}
