using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Loca.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class VenueRepository(LocaDbContext context) : IVenueRepository
{
    public Task<Venue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Venues
            .Include(venue => venue.City)
            .Include(venue => venue.Halls)
            .FirstOrDefaultAsync(venue => venue.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Venue> Items, int TotalCount)> GetPagedAsync(
        Guid? cityId,
        string? arama,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Salonlar da yukleniyor cunku liste satirinda salon sayisi gosteriliyor.
        // Yuklenmeseydi navigation bos gelir ve sayi her mekanda sessizce
        // sifir gorunurdu — hata vermeyen, yalnizca yanlis olan bir sonuc.
        var sorgu = context.Venues
            .Include(venue => venue.City)
            .Include(venue => venue.Halls)
            .AsNoTracking()
            .AsQueryable();

        if (cityId is { } sehir)
            sorgu = sorgu.Where(venue => venue.CityId == sehir);

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var kalip = LikePattern.Contains(arama);

            sorgu = sorgu.Where(venue =>
                EF.Functions.ILike(venue.Name, kalip) ||
                EF.Functions.ILike(venue.Address, kalip));
        }

        // Sayim sayfalamadan ONCE yapilir: toplam kayit sayisi sayfa
        // buyuklugunden bagimsizdir.
        var toplam = await sorgu.CountAsync(cancellationToken);

        var kayitlar = await sorgu
            .OrderBy(venue => venue.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (kayitlar, toplam);
    }

    public Task<bool> NameExistsAsync(
        Guid cityId,
        string name,
        Guid? haricId = null,
        CancellationToken cancellationToken = default)
    {
        var kalip = LikePattern.Escape(name.Trim());

        return context.Venues.AnyAsync(
            venue =>
                venue.CityId == cityId &&
                EF.Functions.ILike(venue.Name, kalip) &&
                (haricId == null || venue.Id != haricId),
            cancellationToken);
    }

    public Task<bool> HasHallsAsync(Guid venueId, CancellationToken cancellationToken = default) =>
        context.Halls.AnyAsync(hall => hall.VenueId == venueId, cancellationToken);

    public Task<bool> CityExistsAsync(Guid cityId, CancellationToken cancellationToken = default) =>
        context.Cities.AnyAsync(city => city.Id == cityId && city.IsActive, cancellationToken);

    public void Add(Venue venue) => context.Venues.Add(venue);

    public void Remove(Venue venue) => context.Venues.Remove(venue);
}
