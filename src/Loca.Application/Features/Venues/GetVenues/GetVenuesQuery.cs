using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Venues.GetVenues;

public sealed record GetVenuesQuery(
    Guid? CityId,
    string? Arama,
    PaginationRequest Pagination) : IRequest<Result<PagedResult<VenueListItem>>>;

internal sealed class GetVenuesQueryHandler(IVenueRepository venues)
    : IRequestHandler<GetVenuesQuery, Result<PagedResult<VenueListItem>>>
{
    public async Task<Result<PagedResult<VenueListItem>>> Handle(
        GetVenuesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (kayitlar, toplam) = await venues.GetPagedAsync(
            request.CityId,
            request.Arama,
            request.Pagination.Skip,
            request.Pagination.PageSize,
            cancellationToken);

        var liste = kayitlar
            .Select(venue => new VenueListItem(
                venue.Id,
                venue.Name,
                venue.City?.Name ?? string.Empty,
                venue.IsActive,
                venue.Halls.Count))
            .ToList();

        return Result.Success(new PagedResult<VenueListItem>
        {
            Items = liste,
            PageNumber = request.Pagination.PageNumber,
            PageSize = request.Pagination.PageSize,
            TotalCount = toplam
        });
    }
}
