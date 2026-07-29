using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.SeatLayouts.DeleteSeatLayout;

public sealed record DeleteSeatLayoutCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteSeatLayoutCommandHandler(
    ISeatLayoutRepository seatLayouts,
    IUnitOfWork unitOfWork,
    ILogger<DeleteSeatLayoutCommandHandler> logger)
    : IRequestHandler<DeleteSeatLayoutCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSeatLayoutCommand request, CancellationToken cancellationToken)
    {
        var layout = await seatLayouts.GetByIdAsync(
            request.Id, koltuklarlaBirlikte: false, cancellationToken);

        if (layout is null)
            return Result.Failure(SeatLayoutErrors.NotFound);

        // Sartname: "kullanilmis oturma plani fiziksel olarak silinmemelidir."
        // Kullanilmis olup olmadigina bakan kontrol (EventSession referansi)
        // Gun 5'te eklenecek; su an her plan zaten isaretlenerek siliniyor,
        // yani veri hicbir durumda kaybolmuyor.
        seatLayouts.Remove(layout);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Oturma plani silindi (isaretlendi). PlanId: {PlanId}", layout.Id);

        return Result.Success();
    }
}
