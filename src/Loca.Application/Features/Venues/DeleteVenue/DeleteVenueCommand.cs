using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Venues.DeleteVenue;

public sealed record DeleteVenueCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteVenueCommandHandler(
    IVenueRepository venues,
    IUnitOfWork unitOfWork,
    ILogger<DeleteVenueCommandHandler> logger)
    : IRequestHandler<DeleteVenueCommand, Result>
{
    public async Task<Result> Handle(DeleteVenueCommand request, CancellationToken cancellationToken)
    {
        var venue = await venues.GetByIdAsync(request.Id, cancellationToken);

        if (venue is null)
            return Result.Failure(VenueErrors.NotFound);

        // Bagli salon varken silme reddediliyor. Salonlar da silinseydi
        // (zincirleme), planlar ve ilerde onlara bagli etkinlik oturumlari
        // tek bir istekle gorunmez olurdu.
        if (await venues.HasHallsAsync(venue.Id, cancellationToken))
            return Result.Failure(VenueErrors.HasHalls);

        // Fiziksel silme degil: interceptor isaretlemeye ceviriyor.
        venues.Remove(venue);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Mekan silindi (isaretlendi). MekanId: {MekanId}", venue.Id);

        return Result.Success();
    }
}
