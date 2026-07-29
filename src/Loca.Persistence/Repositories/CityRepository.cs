using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class CityRepository(LocaDbContext context) : ICityRepository
{
    public async Task<IReadOnlyList<City>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await context.Cities
            .AsNoTracking()
            .Where(city => city.IsActive)
            .OrderBy(city => city.Name)
            .ToListAsync(cancellationToken);
}
