using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface ICityRepository
{
    /// <summary>Aktif sehirler, ada gore sirali.</summary>
    Task<IReadOnlyList<City>> GetActiveAsync(CancellationToken cancellationToken = default);
}
