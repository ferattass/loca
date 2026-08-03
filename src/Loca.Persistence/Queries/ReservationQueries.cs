using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Reservations.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

/// <summary>
/// Rezervasyon okuma sorgulari; projeksiyonla tek <c>SELECT</c>.
/// </summary>
internal sealed class ReservationQueries(LocaDbContext context) : IReservationQueries
{
    public async Task<ReservationDetail?> GetDetailAsync(
        Guid id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var detay = await context.Reservations
            .Where(reservation => reservation.Id == id)
            .Select(reservation => new ReservationDetail(
                reservation.Id,
                reservation.EventSessionId,
                reservation.EventSession!.EventId,
                reservation.EventSession!.Event!.Title,
                reservation.EventSession!.StartsAtUtc,
                reservation.EventSession!.Event!.Venue!.Name,
                reservation.EventSession!.Hall!.Name,
                reservation.Status,
                reservation.ExpiresAtUtc,
                // Yer tutucu: kalan sure asagida sunucu saatiyle hesaplaniyor.
                // Ifade agacinda TimeSpan aritmetigi PostgreSQL'e cevrilmeye
                // calisilir ve gereksiz yere sorguya girerdi.
                0,
                reservation.ExtensionUsed,
                reservation.TotalAmount.Amount,
                reservation.TotalAmount.Currency,
                reservation.CreatedAt,
                reservation.Items
                    .OrderBy(item => item.EventSeat!.Seat!.RowLabel)
                    .ThenBy(item => item.EventSeat!.Seat!.SeatNumber)
                    .Select(item => new ReservationSeatItem(
                        item.EventSeatId,
                        item.EventSeat!.SeatId,
                        item.EventSeat!.Seat!.SeatSection!.Name,
                        item.EventSeat!.Seat!.RowLabel,
                        item.EventSeat!.Seat!.SeatNumber,
                        item.TicketType!.Name,
                        item.Price.Amount,
                        item.Price.Currency))
                    .ToList()))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return detay is null
            ? null
            : detay with { RemainingSeconds = RemainingSeconds(detay.ExpiresAtUtc, utcNow) };
    }

    public async Task<IReadOnlyList<ReservationListItem>> GetByUserAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var kayitlar = await context.Reservations
            .Where(reservation => reservation.UserId == userId)
            // Yeniden eskiye: kullanici en son yaptigi rezervasyonu aramasin.
            .OrderByDescending(reservation => reservation.CreatedAt)
            .Select(reservation => new ReservationListItem(
                reservation.Id,
                reservation.EventSessionId,
                reservation.EventSession!.EventId,
                reservation.EventSession!.Event!.Title,
                reservation.EventSession!.StartsAtUtc,
                reservation.EventSession!.Event!.Venue!.Name,
                reservation.Status,
                reservation.ExpiresAtUtc,
                0,
                // Alt sorgu olarak ceviriliyor; kalemleri yuklemeye gerek yok.
                reservation.Items.Count,
                reservation.TotalAmount.Amount,
                reservation.TotalAmount.Currency,
                reservation.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return kayitlar
            .Select(kayit => kayit with
            {
                RemainingSeconds = RemainingSeconds(kayit.ExpiresAtUtc, utcNow)
            })
            .ToList();
    }

    /// <summary>
    /// Kilidin bitmesine kalan saniye; dolmussa sifir.
    /// </summary>
    /// <remarks>
    /// <b>Sunucu saatiyle hesaplanir.</b> Istemci <c>ExpiresAtUtc</c> ile kendi
    /// saatini karsilastirsaydi, saati geri alinmis bir makinede sayac hic
    /// bitmez ve kullanici suresi coktan dolmus bir rezervasyonu odemeye
    /// calisirdi.
    /// </remarks>
    private static int RemainingSeconds(DateTime expiresAtUtc, DateTime utcNow) =>
        expiresAtUtc > utcNow
            ? (int)Math.Ceiling((expiresAtUtc - utcNow).TotalSeconds)
            : 0;
}
