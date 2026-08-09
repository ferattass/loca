namespace Loca.WebApi.Middleware;

/// <summary>
/// Tarayiciya "bu yaniti nasil ele alma" talimatlarini ekler.
/// </summary>
/// <remarks>
/// Bu basliklarin tamami <b>tarayici tarafinda</b> calisan savunmalar;
/// sunucudaki yetki kontrollerinin yerine gecmiyorlar, onlarin
/// atlanabildigi durumlarda ikinci bir engel kuruyorlar.
///
/// <para>
/// <b>CSP tek bir politika DEGIL.</b> Bu sunucu iki farkli seyi birden
/// servis ediyor: JSON donen API uclari ve <c>wwwroot</c>'tan gelen SPA.
/// Ikisinin ihtiyaci taban tabana zit — API'nin hicbir kaynak yuklememesi
/// gerekiyor, SPA'nin ise kendi betigini ve stilini yuklemesi. Tek politika
/// yazilirsa biri mutlaka kirilir.
/// </para>
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Kendi HTML'ini, stilini ve betigini getiren panolar.
    /// </summary>
    /// <remarks>
    /// Ikisi de yalnizca gelistirmede (Swagger) veya yalnizca admin'e
    /// (Hangfire) acik oldugu icin istisna dar kaliyor: politika hic
    /// yazilmiyor.
    /// </remarks>
    private static readonly string[] PanoYollari = ["/swagger", "/hangfire"];

    /// <summary>
    /// JSON donen yollar. Geri kalan her sey SPA sayiliyor.
    /// </summary>
    /// <remarks>
    /// Ayrimin yonu bilincli: liste API tarafini sayiyor, SPA tarafini
    /// degil. SPA yollari sayilsaydi (<c>/</c>, <c>/biletlerim</c>,
    /// <c>/assets</c>, ...) her yeni rotada bu listeyi guncellemek
    /// gerekirdi ve unutulan rota bombos bir sayfa olarak acilirdi —
    /// sessiz ve yalnizca tarayicida gorunen bir hata. Yeni bir API ucu
    /// unutulursa sonuc yalnizca gereginden genis bir politika olur.
    /// </remarks>
    private static readonly string[] ApiYollari = ["/api", "/health"];

    /// <summary>
    /// JSON servisi: hicbir kaynak yuklenmemeli, hicbir sey cerceveye
    /// alinmamali. Bir sekilde HTML olarak yorumlanan bir yanit olursa
    /// icindeki hicbir sey calisamaz.
    /// </summary>
    private const string ApiPolitikasi = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>
    /// SPA politikasi: her sey kendi kokumuzden, disaridan hicbir sey.
    /// </summary>
    /// <remarks>
    /// <c>'unsafe-inline'</c> yalnizca <c>style-src</c>'de ve gerekli:
    /// bilesenler olculeri calisma aninda hesapliyor (koltuk plani doluluk
    /// cubugu, bolum genisligi, logo en-boy orani) ve bunlar <c>style</c>
    /// niteligi olarak yaziliyor — CSP satir ici stil niteligini de
    /// engelliyor. <c>script-src</c>'de ayni izin YOK; tehlikeli olan
    /// oradaki satir ici betik, stil degil.
    ///
    /// <para>
    /// <c>data:</c> ve <c>blob:</c> yalnizca goruntu icin: QR kodu ve
    /// indirilen bilet gorseli istemcide uretiliyor.
    /// </para>
    ///
    /// <para>
    /// <c>connect-src 'self'</c> yeterli cunku API ayni kokten servis
    /// ediliyor — tek konteyner karari tam olarak bunu sagliyordu.
    /// </para>
    /// </remarks>
    private const string ArayuzPolitikasi =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var basliklar = context.Response.Headers;
        var yol = context.Request.Path;

        // Tarayici icerik turunu TAHMIN ETMESIN. Tahmin acikken,
        // kullanicinin yukledigi bir dosya "aslinda HTML'e benziyor" diye
        // HTML olarak calistirilabiliyor. Dosya yukleme ucumuz oldugu icin
        // bu somut bir risk.
        basliklar["X-Content-Type-Options"] = "nosniff";

        // Cerceve icine alinamaz. Oturum cerezle tasinsaydi tiklama
        // hirsizligina (clickjacking) acik olurdu; token bellekte
        // tutuldugu icin risk dusuk ama kapatmanin maliyeti de sifir.
        basliklar["X-Frame-Options"] = "DENY";

        // Baska bir siteye gidildiginde adres satirinin tamami
        // gonderilmesin: sifre sifirlama baglantisi gibi adresler token
        // tasiyor ve referrer'a dusen bir token ucuncu tarafa gitmis olur.
        basliklar["Referrer-Policy"] = "no-referrer";

        // Uygulamanin kamera, mikrofon, konum gibi ozelliklere ihtiyaci yok.
        basliklar["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (!YolBasliyorMu(yol, PanoYollari))
        {
            basliklar["Content-Security-Policy"] =
                YolBasliyorMu(yol, ApiYollari) ? ApiPolitikasi : ArayuzPolitikasi;
        }

        await next(context);
    }

    private static bool YolBasliyorMu(PathString yol, string[] onekler)
    {
        foreach (var onek in onekler)
        {
            if (yol.StartsWithSegments(onek, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
