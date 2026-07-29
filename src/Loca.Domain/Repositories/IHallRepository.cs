using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface IHallRepository
{
    Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hall>> GetByVenueIdAsync(
        Guid venueId, CancellationToken cancellationToken = default);

    /// <param name="haricId">Guncelleme sirasinda kaydin kendisi disarida birakilir.</param>
    Task<bool> NameExistsAsync(
        Guid venueId, string name, Guid? haricId = null, CancellationToken cancellationToken = default);

    /// <summary>Salona bagli silinmemis oturma plani var mi.</summary>
    Task<bool> HasSeatLayoutsAsync(Guid hallId, CancellationToken cancellationToken = default);

    void Add(Hall hall);

    /// <inheritdoc cref="IVenueRepository.Remove"/>
    void Remove(Hall hall);
}
