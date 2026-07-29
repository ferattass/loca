using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.SeatLayouts.ToggleSeatActive;

/// <summary>
/// Koltugu satisa acar veya kapatir.
/// </summary>
/// <remarks>
/// Bozuk koltuk, kolon arkasi veya teknik ekip icin ayrilmis koltuk
/// pasiflestirilir. Silinmez: koltuk fiziksel olarak yerinde duruyor ve
/// gecmis satislar bu satira referans veriyor.
/// </remarks>
public sealed record ToggleSeatActiveCommand(Guid SeatId) : IRequest<Result<bool>>;

internal sealed class ToggleSeatActiveCommandHandler(
    ISeatLayoutRepository seatLayouts,
    IUnitOfWork unitOfWork,
    ILogger<ToggleSeatActiveCommandHandler> logger)
    : IRequestHandler<ToggleSeatActiveCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ToggleSeatActiveCommand request, CancellationToken cancellationToken)
    {
        var seat = await seatLayouts.GetSeatByIdAsync(request.SeatId, cancellationToken);

        if (seat is null)
            return Result.Failure<bool>(SeatLayoutErrors.SeatNotFound);

        seat.ToggleActive();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Koltuk durumu degistirildi. KoltukId: {KoltukId}, Aktif: {Aktif}",
            seat.Id, seat.IsActive);

        return Result.Success(seat.IsActive);
    }
}
