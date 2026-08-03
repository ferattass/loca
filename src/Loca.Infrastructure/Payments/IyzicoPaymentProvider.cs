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

    // IPaymentService sozlesmesi alici (buyer) ve adres bilgisi tasimiyor
    // (kart verisi gibi bu bilgiler de kapsam disi tutuldu). Iyzico
    // checkout form baslatirken bu alanlari zorunlu kildigindan asagida
    // sabit yer tutucu degerler kullaniliyor. GERCEK ENTEGRASYONDA bu
    // deger musteri kaydindan veya arayuzun genisletilmesiyle gelmeli;
    // aksi halde Iyzico'nun dolandiricilik (fraud) puanlamasi gercek
    // musteri bilgisiyle calismaz.
    private const string YerTutucuIp = "1.1.1.1";

    private readonly IyzicoOptions _options = options.Value;

    public string Name => "Iyzico";

    public async Task<PaymentResult> CreatePaymentAsync(
        Guid paymentId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        const string Yol = "/payment/iyzipos/checkoutform/initialize/auth/ecom";

        var fiyat = FormatFiyat(amount);
        var tanimlayici = paymentId.ToString("N");

        var istek = new CheckoutFormInitializeRequest
        {
            Locale = Locale,
            ConversationId = paymentId.ToString(),
            Price = fiyat,
            PaidPrice = fiyat,
            Currency = currency,
            BasketId = tanimlayici,
            PaymentGroup = "PRODUCT",
            CallbackUrl = _options.CallbackUrl,
            Buyer = YerTutucuAlici(tanimlayici),
            ShippingAddress = YerTutucuAdres(),
            BillingAddress = YerTutucuAdres(),
            BasketItems = [YerTutucuSepetKalemi(tanimlayici, fiyat)]
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
            return PaymentResult.Success(yanit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Iyzico odeme baslatma cagrisi basarisiz. OdemeId: {OdemeId}", paymentId);
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
            Ip = YerTutucuIp
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
            Ip = YerTutucuIp
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

    private static Buyer YerTutucuAlici(string tanimlayici) => new()
    {
        Id = tanimlayici,
        Name = "Musteri",
        Surname = "Musteri",
        GsmNumber = "+905000000000",
        Email = "odeme@loca.app",
        IdentityNumber = "11111111111",
        RegistrationAddress = "Belirtilmedi",
        Ip = YerTutucuIp,
        City = "Istanbul",
        Country = "Turkiye"
    };

    private static IyzicoAddress YerTutucuAdres() => new()
    {
        ContactName = "Musteri Musteri",
        City = "Istanbul",
        Country = "Turkiye",
        Address = "Belirtilmedi"
    };

    private static BasketItem YerTutucuSepetKalemi(string tanimlayici, string fiyat) => new()
    {
        Id = tanimlayici,
        Name = "Rezervasyon",
        Category1 = "Bilet",
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
