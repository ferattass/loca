using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Bir oturumun rezervasyon akisi icin gereken bilgileri tek satirda tasir.
/// </summary>
/// <remarks>
/// Oturumun kendisi yerine bu kayit doniyor cunku karar icin gereken
/// etkinligin durumu ve satis penceresi iki ayri tabloda. Entity yuklenip
/// navigation uzerinden gidilseydi ayni bilgi icin ikinci bir sorgu olurdu.
/// </remarks>
public sealed record ReservationSessionInfo(
    Guid EventSessionId,
    Guid EventId,
    bool EventIsPublished,
    bool SessionIsCancelled,
    bool SeatsGenerated,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc);

public interface IReservationRepository
{
    /// <summary>Rezervasyonu kalemleriyle birlikte getirir.</summary>
    Task<Reservation?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ayni kullanicinin ayni anahtarla daha once actigi rezervasyon.
    /// </summary>
    /// <remarks>
    /// Idempotency kontrolunun ilk adimi. Anahtar kullanici bazinda tekil:
    /// iki farkli kullanicinin ayni <c>Idempotency-Key</c> uretmesi
    /// birbirini engellememeli.
    /// </remarks>
    Task<Reservation?> GetByIdempotencyKeyAsync(
        Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanicinin bu oturumda hâlen tuttugu koltuk sayisi.
    /// </summary>
    /// <remarks>
    /// Suresi dolmus bekleyen rezervasyonlar SAYILMAZ; arka plan isi onlari
    /// henuz temizlememis olabilir ve sayilsaydi kullanici, aslinda birakilmis
    /// koltuklar yuzunden limite takilirdi.
    /// </remarks>
    Task<int> CountActiveSeatsAsync(
        Guid userId,
        Guid eventSessionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rezervasyon akisinin oturum on kontrolleri icin tek satirlik ozet.
    /// </summary>
    Task<ReservationSessionInfo?> GetSessionInfoAsync(
        Guid eventSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Secilen koltuklari <b>degisiklik takibiyle</b> getirir.
    /// </summary>
    /// <remarks>
    /// <b>Akisin 5. adimi.</b> Redis kilidini almis olmak koltugun
    /// veritabaninda hâlâ bos oldugunu garanti etmez: kilit Redis'e
    /// ulasamamis olabilir, TTL dolmus olabilir veya koltuk bambaska bir
    /// yoldan satilmis olabilir. Bu okuma transaction icinde yapilir ve
    /// donen satirlar izlenir; ayni satirlar uzerinde <c>UPDATE</c>
    /// uretilecegi icin <c>AsNoTracking</c> KULLANILMAZ.
    /// </remarks>
    Task<IReadOnlyList<EventSeat>> GetSeatsForUpdateAsync(
        Guid eventSessionId,
        IReadOnlyCollection<Guid> eventSeatIds,
        CancellationToken cancellationToken = default);

    /// <summary>Rezervasyona bagli koltuklar; iptal ve sure dolumunda serbest birakilir.</summary>
    Task<IReadOnlyList<EventSeat>> GetSeatsOfReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla rezervasyonun koltuklari, TEK sorguda.
    /// </summary>
    /// <remarks>
    /// Sure dolumu isi bir turda yuze kadar rezervasyon isliyor; her biri
    /// icin ayri sorgu calistirilsaydi tek turda yuz gidis donus olurdu —
    /// Gun 5'te olculen N+1 tuzaginin aynisi.
    /// </remarks>
    Task<IReadOnlyList<EventSeat>> GetSeatsOfReservationsAsync(
        IReadOnlyCollection<Guid> reservationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suresi dolmus bekleyen rezervasyonlar.
    /// </summary>
    /// <param name="batchSize">
    /// Tek turda islenecek en fazla kayit. Sinirsiz olsaydi birikmis
    /// binlerce kayit tek transaction'da islenir ve tablo uzun sure kilitli
    /// kalirdi.
    /// </param>
    Task<IReadOnlyList<Reservation>> GetExpiredAsync(
        DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

    void Add(Reservation reservation);
}
