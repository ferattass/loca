using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Venues.GetVenueById;

public sealed record GetVenueByIdQuery(Guid Id) : IRequest<Result<VenueResponse>>;

internal sealed class GetVenueByIdQueryHandler(IVenueRepository venues)
    : IRequestHandler<GetVenueByIdQuery, Result<VenueResponse>>
{
    public async Task<Result<VenueResponse>> Handle(
        GetVenueByIdQuery request, CancellationToken cancellationToken)
    {
        var venue = await venues.GetByIdAsync(request.Id, cancellationToken);

        if (venue is null)
            return Result.Failure<VenueResponse>(VenueErrors.NotFound);

        var response = new VenueResponse(
            venue.Id,
            venue.CityId,
            venue.City?.Name ?? string.Empty,
            venue.Name,
            venue.Address,
            venue.Description,
            venue.PhoneNumber,
            venue.ImageFileId,
            venue.IsActive,
            [.. venue.Halls.Select(hall =>
                new VenueHallSummary(hall.Id, hall.Name, hall.Capacity, hall.IsActive))]);

        return Result.Success(response);
    }
}
