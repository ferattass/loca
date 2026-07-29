using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface IVenueRepository
{
    Task<Venue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="cityId">Verilirse yalnizca o sehirdeki mekanlar doner.</param>
    /// <param name="arama">Ad veya adres icinde gecen metin.</param>
    Task<(IReadOnlyList<Venue> Items, int TotalCount)> GetPagedAsync(
        Guid? cityId,
        string? arama,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Ayni sehirde ayni adla baska bir mekan var mi.</summary>
    /// <param name="haricId">Guncelleme sirasinda kaydin kendisi disarida birakilir.</param>
    Task<bool> NameExistsAsync(
        Guid cityId, string name, Guid? haricId = null, CancellationToken cancellationToken = default);

    /// <summary>Mekana bagli silinmemis salon var mi.</summary>
    Task<bool> HasHallsAsync(Guid venueId, CancellationToken cancellationToken = default);

    Task<bool> CityExistsAsync(Guid cityId, CancellationToken cancellationToken = default);

    void Add(Venue venue);

    /// <remarks>
    /// Fiziksel silme degil: interceptor bu istegi isaretlemeye cevirir.
    /// Gecmis satis kayitlari etkinlik uzerinden mekana bagli.
    /// </remarks>
    void Remove(Venue venue);
}
