using Loca.Application.Common.Models;
using Loca.Application.Features.Halls.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Halls.DeleteHall;

public sealed record DeleteHallCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteHallCommandHandler(
    IHallRepository halls,
    IUnitOfWork unitOfWork,
    ILogger<DeleteHallCommandHandler> logger)
    : IRequestHandler<DeleteHallCommand, Result>
{
    public async Task<Result> Handle(DeleteHallCommand request, CancellationToken cancellationToken)
    {
        var hall = await halls.GetByIdAsync(request.Id, cancellationToken);

        if (hall is null)
            return Result.Failure(HallErrors.NotFound);

        // Yol haritasi: aktif etkinligi olan salon silinemez. Etkinlikler
        // Gun 5'te geliyor; su an en yakin karsiligi bagli oturma planlari.
        // Etkinlik oturumu eklendiginde kontrol buraya eklenecek.
        if (await halls.HasSeatLayoutsAsync(hall.Id, cancellationToken))
            return Result.Failure(HallErrors.HasSeatLayouts);

        halls.Remove(hall);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Salon silindi (isaretlendi). SalonId: {SalonId}", hall.Id);

        return Result.Success();
    }
}
