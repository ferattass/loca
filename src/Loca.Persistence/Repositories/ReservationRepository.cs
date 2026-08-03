using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class ReservationRepository(LocaDbContext context) : IReservationRepository
{
    /// <summary>
    /// Bilet satisina izin veren etkinlik durumlari.
    /// </summary>
    /// <remarks>
    /// <c>Published</c> da listede: yayina alinmis bir etkinligin satisi
    /// oturumun kendi satis penceresiyle aciliyor. Etkinlik durumu kaba
    /// filtre, asil zaman kapisi <c>SalesStartsAtUtc</c>/<c>SalesEndsAtUtc</c>.
    /// Gun 7'de durumu <c>SalesOpen</c>'a cevirecek zamanlanmis is gelecek;
    /// o is gelene kadar yalnizca <c>SalesOpen</c> arasaydik hicbir etkinlikte
    /// bilet satilamazdi.
    /// </remarks>
    private static readonly EventStatus[] SaleableStatuses =
    [
        EventStatus.Published,
        EventStatus.SalesOpen
    ];

    public Task<Reservation?> GetAggregateAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        context.Reservations
            .Include(reservation => reservation.Items)
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public Task<Reservation?> GetByIdempotencyKeyAsync(
        Guid userId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        context.Reservations.FirstOrDefaultAsync(
            reservation =>
                reservation.UserId == userId &&
                reservation.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<int> CountActiveSeatsAsync(
        Guid userId,
        Guid eventSessionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        // Rezervasyonlar uzerinden degil KALEMLER uzerinden sayiliyor:
        // limit "kac rezervasyon" degil "kac bilet". Iki rezervasyonda uceer
        // koltuk tutan kullanici alti bilete ulasmis olur.
        await context.ReservationItems.CountAsync(
            item =>
                item.Reservation!.UserId == userId &&
                item.Reservation!.EventSessionId == eventSessionId &&
                (item.Reservation!.Status == ReservationStatus.Confirmed ||
                 (item.Reservation!.Status == ReservationStatus.Pending &&
                  item.Reservation!.ExpiresAtUtc > utcNow)),
            cancellationToken);

    public Task<ReservationSessionInfo?> GetSessionInfoAsync(
        Guid eventSessionId, CancellationToken cancellationToken = default) =>
        // Tek sorgu, tek satir. Oturum entity'si yuklenip navigation
        // uzerinden etkinlige gidilseydi ayni bilgi icin ikinci gidis donus
        // olurdu.
        context.EventSessions
            .Where(session => session.Id == eventSessionId)
            .Select(session => new ReservationSessionInfo(
                session.Id,
                session.EventId,
                SaleableStatuses.Contains(session.Event!.Status),
                session.Status == EventSessionStatus.Cancelled,
                session.SeatsGenerated,
                session.SalesStartsAtUtc,
                session.SalesEndsAtUtc))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EventSeat>> GetSeatsForUpdateAsync(
        Guid eventSessionId,
        IReadOnlyCollection<Guid> eventSeatIds,
        CancellationToken cancellationToken = default) =>
        await context.EventSeats
            // Bilet turu ogrenci belgesi isteyip istemedigi icin gerekli.
            // Ayri sorguyla cekilseydi koltuk sayisi kadar gidis donus olurdu.
            .Include(seat => seat.TicketType)
            .Where(seat =>
                seat.EventSessionId == eventSessionId &&
                eventSeatIds.Contains(seat.Id))
            // AsNoTracking YOK ve olmamali: bu satirlarin durumu birazdan
            // guncellenecek. Takip disi okunsalardi degisiklikler
            // SaveChanges'e hic ulasmaz, istek basarili gorunur ve koltuk
            // kilitlenmemis olurdu.
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EventSeat>> GetSeatsOfReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        // HÂLÂ bu rezervasyona bagli olanlar. Kilit suresi dolup koltuk
        // baskasina gectiyse ReservationId artik bu kayda isaret etmez ve
        // koltuk buraya girmez — iptal ve sure dolumu, baskasinin koltugunu
        // elinden almasin.
        await context.EventSeats
            .Where(seat => seat.ReservationId == reservationId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EventSeat>> GetSeatsOfReservationsAsync(
        IReadOnlyCollection<Guid> reservationIds, CancellationToken cancellationToken = default) =>
        await context.EventSeats
            .Where(seat =>
                seat.ReservationId != null &&
                reservationIds.Contains(seat.ReservationId.Value))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> GetExpiredAsync(
        DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Reservations
            .Where(reservation =>
                reservation.Status == ReservationStatus.Pending &&
                reservation.ExpiresAtUtc <= utcNow)
            // En eskiden baslanir: birikme durumunda en uzun sure bloke kalan
            // koltuklar once serbest kalsin.
            .OrderBy(reservation => reservation.ExpiresAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UpcomingReservation>> GetUpcomingForReminderAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        await context.Reservations
            .Where(reservation =>
                reservation.Status == ReservationStatus.Confirmed &&
                reservation.EventSession!.StartsAtUtc >= fromUtc &&
                reservation.EventSession!.StartsAtUtc < toUtc)
            .Select(reservation => new UpcomingReservation(
                reservation.Id,
                reservation.UserId,
                reservation.EventSessionId,
                reservation.EventSession!.Event!.Title,
                reservation.EventSession!.Event!.Venue!.Name,
                reservation.EventSession!.StartsAtUtc,
                reservation.Items.Count))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <remarks>
    /// Burada Gun 5'teki "EF yeni kaydi UPDATE etti" tuzagi YOK: orada sorun,
    /// zaten izlenen bir aggregate'in koleksiyonuna eklenen cocuk nesneydi.
    /// Rezervasyon kok nesne olarak ekleniyor ve grafigin tamami (kalemler
    /// dahil) <c>Added</c> isaretleniyor.
    /// </remarks>
    public void Add(Reservation reservation) => context.Reservations.Add(reservation);
}
