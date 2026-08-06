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
/// API JSON donduren bir servis, HTML degil — o yuzden tam bir CSP yerine
/// her seyi reddeden dar bir politika yaziliyor. Bir sekilde HTML olarak
/// yorumlanan bir yanit olursa icindeki hicbir sey calisamayacak.
/// </para>
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Kendi arayuzunu HTML olarak servis eden yollar.
    /// </summary>
    /// <remarks>
    /// Bu iki pano gercek HTML, stil ve betik yukluyor; asagidaki dar CSP
    /// onlara uygulansaydi sayfalar bombos acilirdi. Ikisi de yalnizca
    /// gelistirmede (Swagger) veya yalnizca admin'e (Hangfire) acik
    /// oldugu icin istisna dar kaliyor.
    /// </remarks>
    private static readonly string[] ArayuzYollari = ["/swagger", "/hangfire"];

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var basliklar = context.Response.Headers;
        var arayuz = ArayuzMu(context.Request.Path);

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

        // API'nin kamera, mikrofon, konum gibi ozelliklere ihtiyaci yok.
        basliklar["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // JSON servisi; hicbir kaynak yuklenmemeli ve hicbir sey
        // cerceveye alinmamali. Panolar kendi HTML'ini servis ettigi icin
        // onlara uygulanmiyor.
        if (!arayuz)
            basliklar["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        await next(context);
    }

    private static bool ArayuzMu(PathString yol)
    {
        foreach (var arayuzYolu in ArayuzYollari)
        {
            if (yol.StartsWithSegments(arayuzYolu, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
