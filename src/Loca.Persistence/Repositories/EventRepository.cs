using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class EventRepository(LocaDbContext context) : IEventRepository
{
    /// <summary>
    /// Geri alinmasi bir kullaniciyi etkileyen koltuk durumlari.
    /// </summary>
    /// <remarks>
    /// Kilitli koltuk buraya girmez: kilit zaten suresi dolunca kendiliginden
    /// dusuyor, on dakikalik bir kilit yuzunden silme veya fiyat degisikligi
    /// engellenirse organizator hicbir zaman is yapamaz. Rezerve ve satilmis
    /// koltuk ise gercek bir taahhut.
    /// </remarks>
    private static readonly EventSeatStatus[] CommittedStatuses =
    [
        EventSeatStatus.Reserved,
        EventSeatStatus.Sold
    ];

    public Task<Event?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Events
            .Include(ev => ev.Sessions)
            .Include(ev => ev.TicketTypes)
            .FirstOrDefaultAsync(ev => ev.Id == id, cancellationToken);

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Events.FirstOrDefaultAsync(ev => ev.Id == id, cancellationToken);

    public Task<bool> CategoryExistsAsync(
        Guid categoryId, CancellationToken cancellationToken = default) =>
        context.EventCategories.AnyAsync(
            category => category.Id == categoryId && category.IsActive, cancellationToken);

    public Task<bool> PlaceIsValidAsync(
        Guid cityId, Guid venueId, Guid hallId, CancellationToken cancellationToken = default) =>
        // Tek sorgu, uc kontrol: salon o mekanin mi, mekan o sehrin mi.
        // Uc ayri Any() cagrisi da ayni sonucu verirdi ama uc gidis donusle.
        context.Halls.AnyAsync(
            hall =>
                hall.Id == hallId &&
                hall.VenueId == venueId &&
                hall.Venue!.CityId == cityId,
            cancellationToken);

    public Task<bool> SeatLayoutBelongsToHallAsync(
        Guid seatLayoutId, Guid hallId, CancellationToken cancellationToken = default) =>
        context.SeatLayouts.AnyAsync(
            layout => layout.Id == seatLayoutId && layout.HallId == hallId && layout.IsActive,
            cancellationToken);

    public Task<bool> HallHasSessionConflictAsync(
        Guid hallId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? haricEtkinlikId = null,
        CancellationToken cancellationToken = default)
    {
        // Temizlik payi iki yana da eklenir: yeni oturum mevcut oturumun
        // hemen ardina da, hemen onune de sikistirilmamali.
        var pay = EventSession.TemizlikPayi;
        var baslangic = startsAtUtc - pay;
        var bitis = endsAtUtc + pay;

        // ARALIK KESISIM MANTIGI. Iki aralik [a1,a2] ve [b1,b2] su kosulda
        // kesisir: a1 < b2 && a2 > b1. Sik yapilan hata "yeni baslangic
        // mevcut aralikta mi" seklindeki tek yonlu kontroldur; o kontrol,
        // yeni araligin mevcut araligi TAMAMEN KAPSADIGI durumu kacirir.
        return context.EventSessions.AnyAsync(
            session =>
                session.HallId == hallId &&
                session.Status != EventSessionStatus.Cancelled &&
                (haricEtkinlikId == null || session.EventId != haricEtkinlikId) &&
                baslangic < session.EndsAtUtc &&
                bitis > session.StartsAtUtc,
            cancellationToken);
    }

    public async Task<int> HallCapacityAsync(
        Guid hallId, CancellationToken cancellationToken = default) =>
        await context.Halls
            .Where(hall => hall.Id == hallId)
            .Select(hall => hall.Capacity)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> TotalQuotaAsync(
        Guid eventId,
        Guid? haricTicketTypeId = null,
        CancellationToken cancellationToken = default) =>
        await context.TicketTypes
            .Where(ticketType =>
                ticketType.EventId == eventId &&
                ticketType.IsActive &&
                (haricTicketTypeId == null || ticketType.Id != haricTicketTypeId))
            .SumAsync(ticketType => ticketType.Quota, cancellationToken);

    public Task<bool> HasCommittedSeatsAsync(
        Guid eventId, CancellationToken cancellationToken = default) =>
        context.EventSeats.AnyAsync(
            seat =>
                seat.EventSession!.EventId == eventId &&
                CommittedStatuses.Contains(seat.Status),
            cancellationToken);

    public Task<bool> TicketTypeHasCommittedSeatsAsync(
        Guid ticketTypeId, CancellationToken cancellationToken = default) =>
        context.EventSeats.AnyAsync(
            seat =>
                seat.TicketTypeId == ticketTypeId &&
                CommittedStatuses.Contains(seat.Status),
            cancellationToken);

    public async Task<IReadOnlyList<SeatPlacement>> GetActiveSeatPlacementsAsync(
        Guid seatLayoutId, CancellationToken cancellationToken = default) =>
        await context.Seats
            .Where(seat => seat.SeatSection!.SeatLayoutId == seatLayoutId && seat.IsActive)
            .Select(seat => new SeatPlacement(seat.Id, seat.SeatSectionId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<bool> SeatsGeneratedForSessionAsync(
        Guid eventSessionId, CancellationToken cancellationToken = default) =>
        context.EventSeats.AnyAsync(
            seat => seat.EventSessionId == eventSessionId, cancellationToken);

    public void AddEventSeats(IReadOnlyList<EventSeat> eventSeats) =>
        context.EventSeats.AddRange(eventSeats);

    public Task<EventSession?> GetSessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default) =>
        context.EventSessions.FirstOrDefaultAsync(
            session => session.Id == sessionId, cancellationToken);

    public Task<TicketType?> GetTicketTypeAsync(
        Guid ticketTypeId, CancellationToken cancellationToken = default) =>
        context.TicketTypes.FirstOrDefaultAsync(
            ticketType => ticketType.Id == ticketTypeId, cancellationToken);

    public Task<bool> SectionBelongsToHallAsync(
        Guid seatSectionId, Guid hallId, CancellationToken cancellationToken = default) =>
        context.SeatSections.AnyAsync(
            section =>
                section.Id == seatSectionId &&
                section.SeatLayout!.HallId == hallId,
            cancellationToken);

    public void Add(Event ev) => context.Events.Add(ev);

    public void AddSession(EventSession session) => context.EventSessions.Add(session);

    public void AddTicketType(TicketType ticketType) => context.TicketTypes.Add(ticketType);

    public void AddAuditLog(AuditLog auditLog) => context.AuditLogs.Add(auditLog);

    public void Remove(Event ev) => context.Events.Remove(ev);

    public void RemoveTicketType(TicketType ticketType) => context.TicketTypes.Remove(ticketType);
}
