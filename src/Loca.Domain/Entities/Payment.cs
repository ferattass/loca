using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Bir rezervasyonun odemesi.
/// </summary>
/// <remarks>
/// Tutar rezervasyondan <b>kopyalanir</b>, referans verilmez: rezervasyon
/// kalemleri bir sekilde degistirilse bile tahsil edilen tutar degismemeli.
///
/// <para>
/// <b>Callback'ler idempotent.</b> Odeme saglayicilari ayni bildirimi birden
/// fazla kez gonderir — ag hatasi, yeniden deneme, elle tetikleme. Ikinci
/// bildirim hata olarak islenirse kullanici basarili bir odemenin ardindan
/// hata ekrani gorur; sessizce yeniden islenirse ikinci kez bilet uretilir.
/// Bu yuzden <see cref="Complete"/> ve <see cref="Fail"/> "durum degisti mi"
/// bilgisini donuyor: cagiran taraf yalnizca DEGISTIYSE bilet uretimi gibi
/// yan etkileri calistiriyor.
/// </para>
/// </remarks>
public sealed class Payment : BaseEntity, IAggregateRoot, IOwnedResource
{
    private readonly List<PaymentTransaction> _transactions = [];

    private Payment()
    {
        Provider = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public Payment(
        Guid reservationId,
        Guid userId,
        Money amount,
        string provider,
        string idempotencyKey,
        DateTime utcNow)
    {
        if (reservationId == Guid.Empty)
            throw new DomainException("Odeme bir rezervasyona bagli olmali.");

        if (userId == Guid.Empty)
            throw new DomainException("Odeme bir kullaniciya bagli olmali.");

        if (string.IsNullOrWhiteSpace(provider))
            throw new DomainException("Odeme saglayicisi zorunludur.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency anahtari zorunludur.");

        if (amount.Amount <= 0)
            throw new DomainException("Odeme tutari sifirdan buyuk olmali.");

        ReservationId = reservationId;
        UserId = userId;
        Amount = amount;
        Provider = provider.Trim();
        IdempotencyKey = idempotencyKey.Trim();

        _transactions.Add(new PaymentTransaction(
            Id, PaymentTransactionType.Create, utcNow, null, null));
    }

    public Guid ReservationId { get; private set; }
    public Reservation? Reservation { get; private set; }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    /// <summary>Rezervasyon toplamindan kopyalanan tutar.</summary>
    public Money Amount { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    /// <summary>Kullanilan saglayicinin adi ("Mock", "FailedMock").</summary>
    public string Provider { get; private set; }

    /// <summary>Saglayicinin islem kimligi. Mutabakat ve iade icin.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>
    /// Ayni rezervasyon icin ikinci bir odeme baslatilmasini engelleyen anahtar.
    /// </summary>
    public string IdempotencyKey { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public DateTime? RefundedAtUtc { get; private set; }

    /// <remarks>Saglayicidan gelen sebep; kart bilgisi icermez.</remarks>
    public string? FailureReason { get; private set; }

    public IReadOnlyList<PaymentTransaction> Transactions => _transactions;

    Guid IOwnedResource.OwnerId => UserId;

    /// <summary>Sonuc bekleniyor mu.</summary>
    public bool IsPending => Status == PaymentStatus.Pending;

    /// <summary>Bilet uretimini hak eden durum.</summary>
    public bool IsSuccessful => Status == PaymentStatus.Succeeded;

    /// <summary>
    /// Odemeyi basarili olarak kapatir.
    /// </summary>
    /// <returns>
    /// Durum bu cagriyla degistiyse <c>true</c>; odeme zaten basariliysa
    /// <c>false</c>. <b>Ikinci callback hata degildir</b>, tekrar edilmis bir
    /// bildirimdir; <c>false</c> donmesi cagiran tarafin bileti ikinci kez
    /// uretmesini engeller.
    /// </returns>
    public bool Complete(string? providerReference, DateTime utcNow)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            // Tekrar eden bildirim. Saglayici kimligi de UZERINE YAZILMIYOR:
            // ilk basarili bildirim esas alinir, sonrakiler kaydi degistiremez.
            return false;
        }

        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Bu durumdaki odeme tamamlanamaz: {Status}.");

        Status = PaymentStatus.Succeeded;
        CompletedAtUtc = utcNow;
        ProviderReference = providerReference;
        FailureReason = null;

        _transactions.Add(new PaymentTransaction(
            Id, PaymentTransactionType.Complete, utcNow, providerReference, null));

        return true;
    }

    /// <summary>
    /// Odemeyi basarisiz olarak kapatir.
    /// </summary>
    /// <returns>Durum degistiyse <c>true</c>; zaten basarisizsa <c>false</c>.</returns>
    public bool Fail(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Basarisizlik sebebi zorunludur.");

        if (Status == PaymentStatus.Failed)
            return false;

        // BASARILI ODEME BASARISIZA CEVRILEMEZ. Cevrilebilseydi geciken bir
        // "reddedildi" bildirimi, parasi alinmis ve bileti uretilmis bir
        // rezervasyonu iptal ederdi.
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Bu durumdaki odeme basarisiz olarak isaretlenemez: {Status}.");

        Status = PaymentStatus.Failed;
        FailedAtUtc = utcNow;
        FailureReason = reason.Trim();

        _transactions.Add(new PaymentTransaction(
            Id, PaymentTransactionType.Fail, utcNow, null, FailureReason));

        return true;
    }

    /// <summary>
    /// Tahsil edilmis tutari iade edilmis olarak isaretler.
    /// </summary>
    /// <returns>Durum degistiyse <c>true</c>; zaten iade edilmisse <c>false</c>.</returns>
    public bool Refund(DateTime utcNow, string? reason = null)
    {
        if (Status == PaymentStatus.Refunded)
            return false;

        if (Status != PaymentStatus.Succeeded)
            throw new DomainException("Yalnizca basarili odeme iade edilebilir.");

        Status = PaymentStatus.Refunded;
        RefundedAtUtc = utcNow;

        _transactions.Add(new PaymentTransaction(
            Id, PaymentTransactionType.Refund, utcNow, ProviderReference, reason));

        return true;
    }

    /// <summary>
    /// Sonuclanmamis odemeyi iptal eder (kullanici vazgecti, sure doldu).
    /// </summary>
    /// <returns>Durum degistiyse <c>true</c>.</returns>
    public bool Cancel(DateTime utcNow, string? reason = null)
    {
        if (Status == PaymentStatus.Cancelled)
            return false;

        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Bu durumdaki odeme iptal edilemez: {Status}.");

        Status = PaymentStatus.Cancelled;

        _transactions.Add(new PaymentTransaction(
            Id, PaymentTransactionType.Cancel, utcNow, null, reason));

        return true;
    }
}
