using Loca.Application.Common.Models;
using Loca.Application.Features.Halls.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Halls.GetHallsByVenue;

public sealed record GetHallsByVenueQuery(Guid VenueId)
    : IRequest<Result<IReadOnlyList<HallResponse>>>;

internal sealed class GetHallsByVenueQueryHandler(
    IHallRepository halls,
    IVenueRepository venues)
    : IRequestHandler<GetHallsByVenueQuery, Result<IReadOnlyList<HallResponse>>>
{
    public async Task<Result<IReadOnlyList<HallResponse>>> Handle(
        GetHallsByVenueQuery request, CancellationToken cancellationToken)
    {
        var venue = await venues.GetByIdAsync(request.VenueId, cancellationToken);

        // Mekan yoksa bos liste yerine 404: "mekan yok" ile "mekanda salon
        // yok" farkli durumlar ve arayuzde farkli ekranlar gosteriyor.
        if (venue is null)
            return Result.Failure<IReadOnlyList<HallResponse>>(HallErrors.VenueNotFound);

        var kayitlar = await halls.GetByVenueIdAsync(request.VenueId, cancellationToken);

        IReadOnlyList<HallResponse> liste =
        [
            .. kayitlar.Select(hall => new HallResponse(
                hall.Id, venue.Id, venue.Name, hall.Name, hall.Capacity, hall.IsActive))
        ];

        return Result.Success(liste);
    }
}
