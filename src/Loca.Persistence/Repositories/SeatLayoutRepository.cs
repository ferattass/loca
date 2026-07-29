using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Loca.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class SeatLayoutRepository(LocaDbContext context) : ISeatLayoutRepository
{
    public Task<SeatLayout?> GetByIdAsync(
        Guid id,
        bool koltuklarlaBirlikte = false,
        CancellationToken cancellationToken = default)
    {
        var sorgu = context.SeatLayouts
            .Include(layout => layout.Hall)
            .AsQueryable();

        sorgu = koltuklarlaBirlikte
            ? sorgu.Include(layout => layout.Sections).ThenInclude(section => section.Seats)
            : sorgu.Include(layout => layout.Sections);

        return sorgu.FirstOrDefaultAsync(layout => layout.Id == id, cancellationToken);
    }

    /// <remarks>
    /// Bolumler yukleniyor cunku liste satirinda bolum sayisi gosteriliyor.
    /// Koltuklar yuklenmiyor: liste ekraninda gorunmuyorlar ve 600 koltuklu
    /// bir planda her satir yuzlerce kayit tasirdi.
    /// </remarks>
    public async Task<IReadOnlyList<SeatLayout>> GetByHallIdAsync(
        Guid hallId, CancellationToken cancellationToken = default) =>
        await context.SeatLayouts
            .Include(layout => layout.Sections)
            .AsNoTracking()
            .Where(layout => layout.HallId == hallId)
            .OrderBy(layout => layout.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid hallId,
        string name,
        Guid? haricId = null,
        CancellationToken cancellationToken = default)
    {
        var kalip = LikePattern.Escape(name.Trim());

        return context.SeatLayouts.AnyAsync(
            layout =>
                layout.HallId == hallId &&
                EF.Functions.ILike(layout.Name, kalip) &&
                (haricId == null || layout.Id != haricId),
            cancellationToken);
    }

    public Task<SeatSection?> GetSectionByIdAsync(
        Guid sectionId, CancellationToken cancellationToken = default) =>
        context.SeatSections
            .Include(section => section.SeatLayout)
            .FirstOrDefaultAsync(section => section.Id == sectionId, cancellationToken);

    public Task<bool> SectionNameExistsAsync(
        Guid seatLayoutId,
        string name,
        Guid? haricId = null,
        CancellationToken cancellationToken = default)
    {
        var kalip = LikePattern.Escape(name.Trim());

        return context.SeatSections.AnyAsync(
            section =>
                section.SeatLayoutId == seatLayoutId &&
                EF.Functions.ILike(section.Name, kalip) &&
                (haricId == null || section.Id != haricId),
            cancellationToken);
    }

    /// <remarks>
    /// Koltuklar yuklenmeden sayilir. Sayim icin planin tum koltuklari
    /// bellege cekilseydi kapasite kontrolu 600 satirlik bir okuma olurdu.
    /// </remarks>
    public Task<int> CountSeatsAsync(Guid seatLayoutId, CancellationToken cancellationToken = default) =>
        context.Seats.CountAsync(
            seat => seat.SeatSection!.SeatLayoutId == seatLayoutId, cancellationToken);

    public Task<int> CountSeatsInSectionAsync(
        Guid sectionId, CancellationToken cancellationToken = default) =>
        context.Seats.CountAsync(seat => seat.SeatSectionId == sectionId, cancellationToken);

    public Task<Seat?> GetSeatByIdAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        context.Seats.FirstOrDefaultAsync(seat => seat.Id == seatId, cancellationToken);

    public void Add(SeatLayout seatLayout) => context.SeatLayouts.Add(seatLayout);

    public void AddSection(SeatSection section) => context.SeatSections.Add(section);

    public void AddSeats(IReadOnlyList<Seat> seats) => context.Seats.AddRange(seats);

    public void Remove(SeatLayout seatLayout) => context.SeatLayouts.Remove(seatLayout);
}
