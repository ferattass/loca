using Loca.Domain.Common;
using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Bilet uretimi icin gereken, rezervasyon kaleminin cozulmus hâli.
/// </summary>
/// <remarks>
/// Bilet kesildigi andaki bilgileri KOPYALIYOR (etkinlik adi, koltuk
/// etiketi, tur adi); bu kayit o alanlari tek sorguda toplayip getiriyor.
/// Kalem basina ayri sorgu calistirilsaydi alti koltukluk bir rezervasyonda
/// alti gidis donus olurdu.
/// </remarks>
public sealed record TicketSource(
    Guid ReservationItemId,
    Guid EventSeatId,
    Guid TicketTypeId,
    Guid EventId,
    Guid EventSessionId,
    string EventTitle,
    string SectionName,
    string RowLabel,
    int SeatNumber,
    string TicketTypeName,
    DateTime EventStartsAtUtc,
    Money Price);

/// <summary>
/// Bir gunun satis ozeti.
/// </summary>
/// <remarks>
/// Iade edilen tutar toplamdan DUSULMUYOR, ayri gosteriliyor: "bugun ne
/// kadar sattik" ile "bugun ne kadar iade ettik" farkli sorular ve tek bir
/// net rakama indirilirse iade oraninin arttigi fark edilmez.
/// </remarks>
public sealed record DailySalesSummary(
    int SucceededCount,
    decimal TotalAmount,
    int RefundedCount,
    decimal RefundedAmount,
    int FailedCount,
    string Currency);

public interface IPaymentRepository
{
    Task<Payment?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rezervasyonun BASARILI odemesi varsa doner.
    /// </summary>
    /// <remarks>
    /// "Ayni rezervasyon icin birden fazla basarili odeme olusamaz" kuralinin
    /// uygulama tarafi. Veritabani tarafi kismi tekil index ile bagli.
    /// </remarks>
    Task<Payment?> GetSuccessfulByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Rezervasyonun sonuc bekleyen odemesi. Ikinci kez baslatmayi engeller.</summary>
    Task<Payment?> GetPendingByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Saglayicinin verdigi kimlikle odemeyi bulur.</summary>
    /// <remarks>
    /// Saglayici callback'i yalnizca kendi kimligini biliyor; bizim odeme
    /// kimligimizi tasimiyor. Bu yuzden donus yolu bu sorgudan geciyor.
    /// </remarks>
    Task<Payment?> GetByProviderReferenceAsync(
        string providerReference, CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdempotencyKeyAsync(
        Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Bilet uretimi icin rezervasyon kalemlerinin cozulmus hâli.</summary>
    Task<IReadOnlyList<TicketSource>> GetTicketSourcesAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Verilen aralikta sonuclanan odemelerin ozeti. Gunluk rapor icin.</summary>
    Task<DailySalesSummary> GetDailySummaryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    void Add(Payment payment);

    /// <summary>
    /// Aggregate icinde olusturulmus islem satirlarini EKLEME olarak bildirir.
    /// </summary>
    /// <remarks>
    /// <b>Bu cagri atlanirsa kayit INSERT degil UPDATE olarak gider ve
    /// eszamanlilik hatasi firlar.</b> Gun 5'te bilet turlerinde yasanan
    /// tuzagin aynisi: EF, izlenen bir nesnenin koleksiyonunda yeni bir cocuk
    /// buldugunda "yeni mi" sorusunu birincil anahtara bakarak cevapliyor;
    /// <c>BaseEntity.Id</c> nesne kurulurken <c>Guid.CreateVersion7()</c> ile
    /// doldugu icin anahtar her zaman dolu ve EF hicbir zaman <c>Added</c>
    /// demiyor.
    ///
    /// <para>
    /// <c>Payment.Complete</c>, <c>Fail</c>, <c>Refund</c> ve <c>Cancel</c>
    /// her cagrida bir islem satiri ekliyor; bunlardan sonra cagrilmali.
    /// </para>
    /// </remarks>
    void RegisterNewTransactions(Payment payment);
}
