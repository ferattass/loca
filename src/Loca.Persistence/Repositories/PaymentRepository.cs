using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class PaymentRepository(LocaDbContext context) : IPaymentRepository
{
    public Task<Payment?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Payments
            .Include(payment => payment.Transactions)
            .FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Payment?> GetSuccessfulByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(
            payment =>
                payment.ReservationId == reservationId &&
                payment.Status == PaymentStatus.Succeeded,
            cancellationToken);

    public Task<Payment?> GetPendingByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(
            payment =>
                payment.ReservationId == reservationId &&
                payment.Status == PaymentStatus.Pending,
            cancellationToken);

    public Task<Payment?> GetByIdempotencyKeyAsync(
        Guid userId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(
            payment => payment.UserId == userId && payment.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<IReadOnlyList<TicketSource>> GetTicketSourcesAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        // Tek sorgu: ReservationItem'dan EventSeat -> Seat -> SeatSection ve
        // EventSeat -> EventSession -> Event uzerinden alanlar toplaniyor.
        // Kalem basina ayri sorgu calistirilsaydi alti koltukluk bir
        // rezervasyonda alti gidis donus olurdu.
        await context.ReservationItems
            .Where(item => item.ReservationId == reservationId)
            .Select(item => new TicketSource(
                item.Id,
                item.EventSeatId,
                item.TicketTypeId,
                item.EventSeat!.EventSession!.EventId,
                item.EventSeat!.EventSessionId,
                item.EventSeat!.EventSession!.Event!.Title,
                item.EventSeat!.Seat!.SeatSection!.Name,
                item.EventSeat!.Seat!.RowLabel,
                item.EventSeat!.Seat!.SeatNumber,
                item.TicketType!.Name,
                item.EventSeat!.EventSession!.StartsAtUtc,
                item.Price))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<DailySalesSummary> GetDailySummaryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // Tek sorgu, tek gidis donus. Uc ayri Count/Sum cagrisi da ayni
        // sonucu verirdi ama uc kez tablo taranarak.
        var satirlar = await context.Payments
            .Where(payment => payment.CreatedAt >= fromUtc && payment.CreatedAt < toUtc)
            .GroupBy(payment => payment.Status)
            .Select(grup => new
            {
                Durum = grup.Key,
                Adet = grup.Count(),
                Tutar = grup.Sum(payment => payment.Amount.Amount),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var basarili = satirlar.FirstOrDefault(satir => satir.Durum == PaymentStatus.Succeeded);
        var iade = satirlar.FirstOrDefault(satir => satir.Durum == PaymentStatus.Refunded);
        var basarisiz = satirlar.FirstOrDefault(satir => satir.Durum == PaymentStatus.Failed);

        // Para birimi ayri sorguyla degil ilk kayittan okunuyor; sistem tek
        // para birimiyle calisiyor (coklu para birimi kapsam disi).
        var paraBirimi = await context.Payments
            .Where(payment => payment.CreatedAt >= fromUtc && payment.CreatedAt < toUtc)
            .Select(payment => payment.Amount.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "TRY";

        return new DailySalesSummary(
            basarili?.Adet ?? 0,
            basarili?.Tutar ?? 0m,
            iade?.Adet ?? 0,
            iade?.Tutar ?? 0m,
            basarisiz?.Adet ?? 0,
            paraBirimi);
    }

    public void Add(Payment payment) => context.Payments.Add(payment);

    public void RegisterNewTransactions(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        foreach (var islem in payment.Transactions)
        {
            var kayit = context.Entry(islem);

            // "Modified" gorunen satir gercekte YENIDIR.
            // PaymentTransaction'in degistiren hicbir metodu yok — yazildiktan
            // sonra hicbir alani guncellenmiyor (denetim izi). Dolayisiyla
            // veritabanindan yuklenmis bir satirin Modified olmasi mumkun
            // degil; EF onu Modified sayiyorsa sebebi anahtarin nesne
            // kurulurken zaten dolu olmasi. Bu ayrim olmadan yeni satir
            // UPDATE olarak gider ve sifir satir etkileyip eszamanlilik
            // hatasi firlatir.
            if (kayit.State is EntityState.Modified or EntityState.Detached)
                kayit.State = EntityState.Added;
        }
    }
}
