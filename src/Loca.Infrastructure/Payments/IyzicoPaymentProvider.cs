using System.Globalization;
using System.Text;
using System.Text.Json;
using Loca.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Iyzico checkout form akisiyla calisan odeme saglayicisi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Neden checkout form, dogrudan kart API'si degil:</b> <see cref="IPaymentService"/>
/// sozlesmesinde bilerek kart parametresi yok, cunku sartname kart verisi
/// saklamayi/tasimayi kapsam disi birakiyor. Checkout form akisinda kart
/// bilgisi tarayicidan dogrudan Iyzico'ya gider; sunucumuz yalnizca bir
/// odeme sayfasi token'i baslatir ve sonucu sorgular. Kart verisi hicbir
/// zaman bu sinifin icinden gecmez; bu da PCI-DSS kapsamini daraltir.
/// </para>
/// <para>
/// <b>Neden resmi <c>Iyzipay</c> NuGet paketi degil:</b> o paket senkron
/// <c>HttpWebRequest</c> kullanir ve <see cref="IHttpClientFactory"/> ile
/// yonetilmez (baglanti havuzu, zaman asimi, Polly politikalari gibi
/// altyapi bu paketin disinda kalir). Bunun yerine burada dogrudan
/// <see cref="HttpClient"/> ile IYZWSv2 imzalamasi elle uygulaniyor (bkz.
/// <see cref="IyzicoSignature"/>).
/// </para>
/// <para>
/// <b>Neden sirlar loglanmiyor:</b> <see cref="IyzicoOptions.SecretKey"/>
/// yalnizca imza hesaplamasinda (HMAC anahtari olarak) kullanilir, hicbir
/// log satirina veya <see cref="PaymentResult.FailureReason"/> icine
/// yazilmaz. Iyzico'nun ham cevabi da hicbir yerde oldugu gibi tasinmaz:
/// asagidaki yanit siniflari kasitli olarak dar tutuldu (yalnizca
/// <c>status</c>, <c>errorCode</c>, <c>errorMessage</c> ve islem
/// kimlikleri); kart maskesi, BIN, kart tipi gibi Iyzico'nun yanitinda
/// gelebilecek alanlar bu siniflarda hic tanimli olmadigindan JSON'da
/// gelseler bile nesneye tasinmiyor, dolayisiyla loglara da sizamiyor.
/// </para>
/// </remarks>
internal sealed class IyzicoPaymentProvider(
    HttpClient httpClient,
    IOptions<IyzicoOptions> options,
    ILogger<IyzicoPaymentProvider> logger) : IPaymentService
{
    // Uygulama Turkiye pazari icin calistigindan sabit "tr" yeterli;
    // Iyzico'nun kendisi de zaten yalnizca TR/EN locale kabul ediyor.
    private const string Locale = "tr";

    // IPaymentService tek bir Reference alani tasiyor ama Iyzico iade
    // icin paymentTransactionId, iptal icin paymentId istiyor; ikisi
    // farkli kimlikler. VerifyPaymentAsync basarili oldugunda ikisini bu
    // ayracla birlestirip tek alanda tasiyoruz; Refund/Cancel cagrildiginda
    // geri ayristiriliyor. Bkz. ReferansParcala.
    private const char ReferansAyraci = '|';

    // Iyzico alici IP'sini zorunlu tutuyor ama istek arkada bir vekil
    // sunucudan gelmisse gercek adres bilinemeyebiliyor. Bu durumda islem
    // reddedilmek yerine bu adres gonderiliyor; alternatif, adres okunamadi
    // diye odemeyi hic baslatmamak olurdu.
    private const string BilinmeyenIp = "0.0.0.0";

    // Iyzico kimlik numarasi alanini zorunlu tutuyor. Sistem kimlik numarasi
    // TOPLAMIYOR (analiz belgesinde de yok) ve yalnizca bu alan icin
    // toplamak, veriyi ihtiyactan fazla toplamak olurdu. Iyzico'nun
    // dokumantasyonunda bu alan icin verilen ornek deger kullaniliyor;
    // dolandiricilik puanlamasi bu alan olmadan da ad, e-posta ve IP ile
    // calisiyor.
    private const string KimlikNumarasiYok = "11111111111";

    private readonly IyzicoOptions _options = options.Value;

    public string Name => "Iyzico";

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        const string Yol = "/payment/iyzipos/checkoutform/initialize/auth/ecom";

        var fiyat = FormatFiyat(request.Amount);
        var tanimlayici = request.PaymentId.ToString("N");
        var ip = string.IsNullOrWhiteSpace(request.Customer.IpAddress)
            ? BilinmeyenIp
            : request.Customer.IpAddress;

        var istek = new CheckoutFormInitializeRequest
        {
            Locale = Locale,
            ConversationId = request.PaymentId.ToString(),
            Price = fiyat,
            PaidPrice = fiyat,
            Currency = request.Currency,
            BasketId = tanimlayici,
            PaymentGroup = "PRODUCT",
            CallbackUrl = _options.CallbackUrl,
            Buyer = Alici(request.Customer, ip),
            ShippingAddress = Adres(request.Customer.FullName),
            BillingAddress = Adres(request.Customer.FullName),
            BasketItems = [SepetKalemi(tanimlayici, request.Description, fiyat)]
        };

        try
        {
            var yanit = await IstekGonderAsync<CheckoutFormInitializeResponse>(Yol, istek, cancellationToken);

            if (yanit is null || !BasariliMi(yanit.Status) || string.IsNullOrWhiteSpace(yanit.Token))
            {
                return PaymentResult.Failure(GuvenliHataMesaji(yanit?.ErrorCode, yanit?.ErrorMessage));
            }

            // Reference'a checkout form token'i konur: kullanici odeme
            // sayfasini tamamladiktan sonra VerifyPaymentAsync bu token ile
            // gercek sonucu Iyzico'ya SORAR (callback'e guvenilmez).
            //
            // paymentPageUrl kullanicinin karti girecegi sayfa. Iyzico bunu
            // token'dan tureterek de verebiliyor ama adres yanittan
            // okunuyor: sandbox ile canlinin alan adlari farkli ve adresi
            // kod icinde kurmak, ortam degisiminde sessizce yanlis sayfaya
            // gondermek demek olurdu.
            return PaymentResult.Success(yanit.Token, yanit.PaymentPageUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(
                ex, "Iyzico odeme baslatma cagrisi basarisiz. OdemeId: {OdemeId}", request.PaymentId);
            return PaymentResult.Failure("Odeme saglayicisina ulasilamadi.");
        }
    }

    public async Task<PaymentResult> VerifyPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return PaymentResult.Failure("Dogrulanacak odeme referansi yok.");
        }

        const string Yol = "/payment/iyzipos/checkoutform/auth/ecom/detail";

        var istek = new CheckoutFormRetrieveRequest
        {
            Locale = Locale,
            ConversationId = paymentId.ToString(),
            Token = reference
        };

        try
        {
            var yanit = await IstekGonderAsync<CheckoutFormRetrieveResponse>(Yol, istek, cancellationToken);

            if (yanit is null || !BasariliMi(yanit.Status))
            {
                return PaymentResult.Failure(GuvenliHataMesaji(yanit?.ErrorCode, yanit?.ErrorMessage));
            }

            // "status" API cagrisinin basarisini, "paymentStatus" ise
            // gercek odemenin sonucunu gosterir; ikisi farkli seyler.
            // Cagri basarili donebilir ama kart reddedilmis olabilir.
            if (!string.Equals(yanit.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentResult.Failure(string.Create(
                    CultureInfo.InvariantCulture, $"Odeme tamamlanmadi (durum: {yanit.PaymentStatus ?? "bilinmiyor"})."));
            }

            if (string.IsNullOrWhiteSpace(yanit.PaymentId))
            {
                return PaymentResult.Failure("Iyzico odeme kimligi bos dondu.");
            }

            var islemKimligi = yanit.ItemTransactions?.Count > 0
                ? yanit.ItemTransactions[0].PaymentTransactionId
                : null;

            var birlesikReferans = string.IsNullOrWhiteSpace(islemKimligi)
                ? yanit.PaymentId
                : string.Create(
                    CultureInfo.InvariantCulture, $"{yanit.PaymentId}{ReferansAyraci}{islemKimligi}");

            return PaymentResult.Success(birlesikReferans);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Iyzico odeme dogrulama cagrisi basarisiz. OdemeId: {OdemeId}", paymentId);
            return PaymentResult.Failure("Odeme saglayicisina ulasilamadi.");
        }
    }

    public async Task<PaymentResult> RefundPaymentAsync(
        Guid paymentId, string? reference, decimal amount, CancellationToken cancellationToken = default)
    {
        var (_, islemKimligi) = ReferansParcala(reference);

        if (string.IsNullOrWhiteSpace(islemKimligi))
        {
            return PaymentResult.Failure("Iade icin gerekli islem kimligi bulunamadi.");
        }

        const string Yol = "/payment/refund";

        var istek = new RefundRequest
        {
            Locale = Locale,
            ConversationId = paymentId.ToString(),
            PaymentTransactionId = islemKimligi,
            Price = FormatFiyat(amount),
            // Iade ve iptal sunucudan yapiliyor; ortada bir tarayici yok,
            // dolayisiyla tasinacak bir kullanici adresi de yok.
            Ip = BilinmeyenIp
        };

        try
        {
            var yanit = await IstekGonderAsync<RefundResponse>(Yol, istek, cancellationToken);

            if (yanit is null || !BasariliMi(yanit.Status))
            {
                return PaymentResult.Failure(GuvenliHataMesaji(yanit?.ErrorCode, yanit?.ErrorMessage));
            }

            return PaymentResult.Success(reference!);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Iyzico iade cagrisi basarisiz. OdemeId: {OdemeId}", paymentId);
            return PaymentResult.Failure("Odeme saglayicisina ulasilamadi.");
        }
    }

    public async Task<PaymentResult> CancelPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default)
    {
        var (iyzicoOdemeKimligi, _) = ReferansParcala(reference);

        if (string.IsNullOrWhiteSpace(iyzicoOdemeKimligi))
        {
            return PaymentResult.Failure("Iptal icin gerekli odeme kimligi bulunamadi.");
        }

        const string Yol = "/payment/cancel";

        var istek = new CancelRequest
        {
            Locale = Locale,
            ConversationId = paymentId.ToString(),
            PaymentId = iyzicoOdemeKimligi,
            // Iade ve iptal sunucudan yapiliyor; ortada bir tarayici yok,
            // dolayisiyla tasinacak bir kullanici adresi de yok.
            Ip = BilinmeyenIp
        };

        try
        {
            var yanit = await IstekGonderAsync<CancelResponse>(Yol, istek, cancellationToken);

            if (yanit is null || !BasariliMi(yanit.Status))
            {
                return PaymentResult.Failure(GuvenliHataMesaji(yanit?.ErrorCode, yanit?.ErrorMessage));
            }

            return PaymentResult.Success(reference!);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Iyzico iptal cagrisi basarisiz. OdemeId: {OdemeId}", paymentId);
            return PaymentResult.Failure("Odeme saglayicisina ulasilamadi.");
        }
    }

    /// <summary>
    /// Govdeyi imzalayip Iyzico'ya POST eder ve yaniti dar bir DTO'ya cozer.
    /// </summary>
    /// <remarks>
    /// Ayni serilestirilmis govde dizesi hem imza hesabinda hem de gonderilen
    /// istekte kullanilir; iki ayri serilestirme cagrisi yapilsaydi (teoride
    /// ayni cikti uretseler bile) imza ile govdenin farkli oldugu bir durum
    /// riske girerdi.
    /// </remarks>
    private async Task<TYanit?> IstekGonderAsync<TYanit>(string yol, object govde, CancellationToken cancellationToken)
    {
        var govdeJson = JsonSerializer.Serialize(govde, JsonSecenekleri);
        var rastgeleDeger = IyzicoSignature.RastgeleDeger();

        using var istek = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseUrl + yol))
        {
            Content = new StringContent(govdeJson, Encoding.UTF8, "application/json")
        };

        istek.Headers.TryAddWithoutValidation("x-iyzi-rnd", rastgeleDeger);
        istek.Headers.TryAddWithoutValidation(
            "Authorization",
            IyzicoSignature.YetkilendirmeBasligi(_options.ApiKey, _options.SecretKey, rastgeleDeger, yol, govdeJson));

        using var yanit = await httpClient.SendAsync(istek, cancellationToken);
        var icerik = await yanit.Content.ReadAsStringAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(icerik)
            ? default
            : JsonSerializer.Deserialize<TYanit>(icerik, JsonSecenekleri);
    }

    private static bool BasariliMi(string? status) =>
        string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Iyzico'nun errorCode/errorMessage alanlarindan guvenli bir hata metni
    /// uretir. Bu iki alan disinda Iyzico yanitindan hicbir sey tasinmaz.
    /// </summary>
    private static string GuvenliHataMesaji(string? errorCode, string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return string.IsNullOrWhiteSpace(errorCode)
                ? "Iyzico islemi basarisiz oldu."
                : string.Create(CultureInfo.InvariantCulture, $"Iyzico hata kodu: {errorCode}");
        }

        return string.IsNullOrWhiteSpace(errorCode)
            ? errorMessage
            : string.Create(CultureInfo.InvariantCulture, $"{errorMessage} (kod: {errorCode})");
    }

    private static (string? OdemeKimligi, string? IslemKimligi) ReferansParcala(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return (null, null);
        }

        var parcalar = reference.Split(ReferansAyraci, 2);

        return parcalar.Length == 2 ? (parcalar[0], parcalar[1]) : (parcalar[0], null);
    }

    private static string FormatFiyat(decimal tutar) =>
        tutar.ToString("0.00", CultureInfo.InvariantCulture);

    /// <remarks>
    /// Iyzico ad ile soyadi ayri istiyor, bizde tek alan var. Son bosluktan
    /// bolunuyor; bosluk yoksa soyad alanina adin kendisi yaziliyor —
    /// alan bos birakilamiyor ve uydurma bir soyad koymak kisiyi yanlis
    /// tanimlamak olurdu.
    /// </remarks>
    private static Buyer Alici(PaymentCustomer musteri, string ip)
    {
        var bosluk = musteri.FullName.LastIndexOf(' ');
        var ad = bosluk > 0 ? musteri.FullName[..bosluk] : musteri.FullName;
        var soyad = bosluk > 0 ? musteri.FullName[(bosluk + 1)..] : musteri.FullName;

        return new Buyer
        {
            // Iyzico'daki alici kimligi bizim kullanici kimligimiz: ayni
            // kisinin farkli odemeleri saglayici tarafinda da ayni kisi
            // olarak gorunuyor, dolandiricilik puanlamasi buna bakiyor.
            Id = musteri.UserId.ToString(),
            Name = ad,
            Surname = soyad,
            GsmNumber = "+905000000000",
            Email = musteri.Email,
            IdentityNumber = KimlikNumarasiYok,
            RegistrationAddress = "Belirtilmedi",
            Ip = ip,
            City = "Istanbul",
            Country = "Turkiye"
        };
    }

    /// <remarks>
    /// Adres alanlari zorunlu ama satilan sey dijital bir bilet: teslimat
    /// adresi diye bir sey yok. Iletisim adi gercek, adres satiri
    /// "Belirtilmedi" — uydurma bir sokak adresi yazmak kayitlari
    /// kirletirdi.
    /// </remarks>
    private static IyzicoAddress Adres(string adSoyad) => new()
    {
        ContactName = adSoyad,
        City = "Istanbul",
        Country = "Turkiye",
        Address = "Belirtilmedi"
    };

    private static BasketItem SepetKalemi(string tanimlayici, string aciklama, string fiyat) => new()
    {
        Id = tanimlayici,
        Name = aciklama,
        Category1 = "Bilet",
        // Fiziksel teslimat yok; yanlis tip gonderilseydi Iyzico kargo
        // sureci bekleyen bir islem acardi.
        ItemType = "VIRTUAL",
        Price = fiyat
    };

    // Iyzico alan adlari camelCase (orn. "conversationId"); C# taraflarindaki
    // PascalCase adlar bu politika ile otomatik eslesir, her alan icin ayri
    // JsonPropertyName yazmaya gerek kalmaz.
    private static readonly JsonSerializerOptions JsonSecenekleri = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

// --- Istek govdeleri --------------------------------------------------------
// Bu siniflar yalnizca serilestirilir (Iyzico'ya gonderilir); "required" ile
// isaretli alanlar sayesinde eksik bir zorunlu deger unutulursa derleme
// zamaninda yakalanir.

internal sealed class CheckoutFormInitializeRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string Price { get; init; }
    public required string PaidPrice { get; init; }
    public required string Currency { get; init; }
    public required string BasketId { get; init; }
    public required string PaymentGroup { get; init; }
    public required string CallbackUrl { get; init; }
    public required Buyer Buyer { get; init; }
    public required IyzicoAddress ShippingAddress { get; init; }
    public required IyzicoAddress BillingAddress { get; init; }
    public required List<BasketItem> BasketItems { get; init; }
}

internal sealed class Buyer
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required string GsmNumber { get; init; }
    public required string Email { get; init; }
    public required string IdentityNumber { get; init; }
    public required string RegistrationAddress { get; init; }
    public required string Ip { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
}

internal sealed class IyzicoAddress
{
    public required string ContactName { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public required string Address { get; init; }
}

internal sealed class BasketItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category1 { get; init; }
    public required string ItemType { get; init; }
    public required string Price { get; init; }
}

internal sealed class CheckoutFormRetrieveRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string Token { get; init; }
}

internal sealed class RefundRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string PaymentTransactionId { get; init; }
    public required string Price { get; init; }
    public required string Ip { get; init; }
}

internal sealed class CancelRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string PaymentId { get; init; }
    public required string Ip { get; init; }
}

// --- Yanit govdeleri ---------------------------------------------------------
// Kasitli olarak dar: Iyzico'nun asil yanitinda bulunabilecek kart maskesi,
// BIN, kart tipi gibi alanlar burada hic tanimlanmadi. System.Text.Json
// taninmayan alanlari sessizce yok sayar; bu yuzden o alanlar JSON'da gelse
// bile bu nesnelere hicbir zaman tasinmaz ve yanlislikla loglanamaz.

internal sealed class CheckoutFormInitializeResponse
{
    public string? Status { get; init; }
    public string? Token { get; init; }

    /// <summary>Kullanicinin karti girecegi Iyzico sayfasi.</summary>
    public string? PaymentPageUrl { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class CheckoutFormRetrieveResponse
{
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public string? PaymentId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ItemTransaction>? ItemTransactions { get; init; }
}

internal sealed class ItemTransaction
{
    public string? PaymentTransactionId { get; init; }
}

internal sealed class RefundResponse
{
    public string? Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class CancelResponse
{
    public string? Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
