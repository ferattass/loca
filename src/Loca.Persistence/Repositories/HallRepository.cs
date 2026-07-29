using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Loca.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class HallRepository(LocaDbContext context) : IHallRepository
{
    public Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Halls
            .Include(hall => hall.Venue)
            .FirstOrDefaultAsync(hall => hall.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Hall>> GetByVenueIdAsync(
        Guid venueId, CancellationToken cancellationToken = default) =>
        await context.Halls
            .AsNoTracking()
            .Where(hall => hall.VenueId == venueId)
            .OrderBy(hall => hall.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid venueId,
        string name,
        Guid? haricId = null,
        CancellationToken cancellationToken = default)
    {
        var kalip = LikePattern.Escape(name.Trim());

        return context.Halls.AnyAsync(
            hall =>
                hall.VenueId == venueId &&
                EF.Functions.ILike(hall.Name, kalip) &&
                (haricId == null || hall.Id != haricId),
            cancellationToken);
    }

    public Task<bool> HasSeatLayoutsAsync(Guid hallId, CancellationToken cancellationToken = default) =>
        context.SeatLayouts.AnyAsync(layout => layout.HallId == hallId, cancellationToken);

    public void Add(Hall hall) => context.Halls.Add(hall);

    public void Remove(Hall hall) => context.Halls.Remove(hall);
}
