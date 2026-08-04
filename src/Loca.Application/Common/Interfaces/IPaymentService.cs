namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Odeme saglayicisinin sonucu.
/// </summary>
/// <param name="Reference">Saglayicinin islem kimligi. Mutabakat ve iade icin.</param>
/// <param name="RedirectUrl">
/// Kullanicinin karti girecegi saglayici sayfasi. Taklit saglayicida
/// <c>null</c>.
/// </param>
/// <param name="FailureReason">
/// Basarisizlik sebebi. <b>Kart bilgisi icermez</b> — saglayici cevabi ham
/// hâliyle tasinsaydi kart verisi denetim tablosuna ve loglara sizardi.
/// </param>
/// <remarks>
/// <b><see cref="RedirectUrl"/> ayri bir alan, <see cref="Reference"/>'tan
/// turetilmiyor.</b> Onceki hâlinde adres, referansin "http" ile baslayip
/// baslamadigina bakilarak cikariliyordu; Iyzico'nun referansi bir token
/// oldugu icin adres hicbir zaman uretilmedi ve kullanici odeme sayfasina
/// hic gonderilmedi. Iki farkli bilgi iki farkli alanda durmali.
/// </remarks>
public sealed record PaymentResult(
    bool Succeeded, string? Reference, string? RedirectUrl, string? FailureReason)
{
    public static PaymentResult Success(string reference, string? redirectUrl = null) =>
        new(true, reference, redirectUrl, null);

    public static PaymentResult Failure(string reason) => new(false, null, null, reason);
}

/// <summary>
/// Odemeyi yapan kisi.
/// </summary>
/// <remarks>
/// <b>Kart bilgisi burada da YOK.</b> Bunlar saglayicinin dolandiricilik
/// puanlamasi icin istedigi kimlik alanlari; kart verisiyle ilgisi yok.
///
/// <para>
/// Onceki hâlinde bu bilgiler saglayiciya sabit yer tutucu olarak
/// gonderiliyordu ("Musteri Musteri", 11111111111). Yer tutucu ile calisan
/// bir dolandiricilik puanlamasi her islemi ayni kisi sanar; canliya
/// cikmadan once kapatilmasi gereken bir acikti.
/// </para>
/// </remarks>
/// <param name="IpAddress">
/// Istegi yapan tarayicinin adresi. Bilinmiyorsa <c>null</c>; saglayici
/// zorunlu tutuyorsa uygulama tarafi kendi degerini koyar.
/// </param>
public sealed record PaymentCustomer(
    Guid UserId,
    string FullName,
    string Email,
    string? IpAddress);

/// <summary>Saglayicida acilacak odeme islemi.</summary>
public sealed record PaymentRequest(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    PaymentCustomer Customer,
    /// <summary>Saglayicinin sepet satirinda gorunecek metin.</summary>
    string Description);

/// <summary>
/// Odeme saglayicisi sozlesmesi.
/// </summary>
/// <remarks>
/// Sartname gercek saglayici entegrasyonunu kapsam disi birakiyor; taklit
/// (mock) uygulama yeterli. Yine de araya bu arayuz konuluyor: gercek
/// saglayici geldiginde degisecek tek yer bu arayuzun uygulamasi olacak,
/// odeme akisinin kendisi degil.
///
/// <para>
/// <b>Kart bilgisi bu arayuzden GECMIYOR.</b> Gercek entegrasyonda kart
/// verisi istemciden dogrudan saglayiciya gider ve sunucu yalnizca sonucu
/// gorur. Arayuze kart parametresi eklenseydi, sonradan o veriyi loglamamak
/// veya saklamamak icin ayrica ugrasilirdi.
/// </para>
/// </remarks>
public interface IPaymentService
{
    /// <summary>Saglayicinin adi. Odeme kaydina yazilir.</summary>
    string Name { get; }

    /// <summary>Odemeyi baslatir; saglayici tarafinda bir islem acar.</summary>
    Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saglayicidaki islemin gercek durumunu sorar.
    /// </summary>
    /// <remarks>
    /// Callback'e guvenilmez: bildirim kaybolabilir, gecikebilir veya taklit
    /// edilebilir. Odeme tamamlanmadan once durum saglayiciya SORULUR.
    /// </remarks>
    Task<PaymentResult> VerifyPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default);

    Task<PaymentResult> RefundPaymentAsync(
        Guid paymentId, string? reference, decimal amount, CancellationToken cancellationToken = default);

    Task<PaymentResult> CancelPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bilet numarasi ve QR kodu ureticisi.
/// </summary>
/// <remarks>
/// Domain'de degil arayuz arkasinda: rastgelelik iceren kod domain'e
/// girseydi bilet uretimi testte deterministik olmazdi. Ayni sebeple
/// <c>IPasswordResetTokenGenerator</c> de altyapida duruyor.
/// </remarks>
public interface ITicketCodeGenerator
{
    /// <summary>Kullaniciya gosterilen bilet numarasi: <c>LOCA-7K3M-9P2Q</c>.</summary>
    string NewTicketNumber();

    /// <summary>
    /// Giriste okutulan kod.
    /// </summary>
    /// <remarks>
    /// Tahmin edilemez olmali: sirali veya kisa bir kod uretilseydi, baskasinin
    /// bilet kodunu deneyerek bulmak mumkun olurdu.
    /// </remarks>
    string NewQrCode();
}
