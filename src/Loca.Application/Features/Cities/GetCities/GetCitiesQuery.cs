using Loca.Application.Common.Models;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Cities.GetCities;

public sealed record CityResponse(Guid Id, string Name, string PlateCode);

public sealed record GetCitiesQuery : IRequest<Result<IReadOnlyList<CityResponse>>>;

internal sealed class GetCitiesQueryHandler(ICityRepository cities)
    : IRequestHandler<GetCitiesQuery, Result<IReadOnlyList<CityResponse>>>
{
    public async Task<Result<IReadOnlyList<CityResponse>>> Handle(
        GetCitiesQuery request, CancellationToken cancellationToken)
    {
        var kayitlar = await cities.GetActiveAsync(cancellationToken);

        IReadOnlyList<CityResponse> liste =
        [
            .. kayitlar.Select(city => new CityResponse(city.Id, city.Name, city.PlateCode))
        ];

        return Result.Success(liste);
    }
}
