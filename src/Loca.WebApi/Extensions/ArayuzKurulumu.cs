namespace Loca.WebApi.Extensions;

/// <summary>
/// Tek servis dagitiminda arayuzun API tarafindan servis edilmesi.
/// </summary>
/// <remarks>
/// Arayuz derlemesi wwwroot'a kopyalanmissa API onu da servis ediyor.
/// Ucretsiz barindirma katmanlarinda servis sayisi sinirli ve iki ayri
/// servis calistirmak hem ikinci bir uyanma suresi hem de CORS ve
/// iyzico callback'i icin ikinci bir alan adi demek. Tek kokten servis
/// edildiginde arayuz ile API ayni origin'de oluyor: CORS devre disi
/// kaliyor ve callback adresi tahmin edilebilir hâle geliyor.
///
/// <para>
/// Klasor yoksa (yerel gelistirme, konteyner yigini) bu blok hic
/// calismiyor ve arayuz eskisi gibi Vite'tan geliyor.
/// </para>
/// </remarks>
public static class ArayuzKurulumu
{
    /// <summary>Sunucuda karsiligi olmayan SPA yollarinin index.html'e dusmedigi on ekler.</summary>
    private static readonly string[] SunucuYollari = ["/api", "/health", "/hangfire", "/swagger"];

    /// <summary>
    /// wwwroot'ta bir arayuz derlemesi varsa statik dosyalari ve SPA geri dusumunu acar.
    /// </summary>
    public static WebApplication ArayuzuServisEt(this WebApplication app)
    {
        var arayuzKlasoru = Path.Combine(app.Environment.ContentRootPath, "wwwroot");

        if (!Directory.Exists(arayuzKlasoru) ||
            !File.Exists(Path.Combine(arayuzKlasoru, "index.html")))
        {
            return app;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // SPA derin yollari (/biletlerim, /yonetim/odemeler) sunucuda karsiligi
        // olmayan adresler; index.html donmezse dogrudan acilan her baglanti
        // 404 alirdi. API ve saglik yollari HARIC tutuluyor, aksi hâlde
        // tanimsiz bir API ucu JSON hatasi yerine HTML donerdi ve istemci
        // "beklenmeyen token <" diye anlasilmaz bir hata gorurdu.
        app.MapFallback(async context =>
        {
            var yol = context.Request.Path.Value ?? string.Empty;

            if (SunucuYollari.Any(on => yol.StartsWith(on, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(Path.Combine(arayuzKlasoru, "index.html"));
        });

        return app;
    }
}
