using Loca.Application.Common.Authentication;
using Loca.WebApi.Middleware;
using Serilog;
using Serilog.Events;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Istek hattinin sirasi ve uclarin baglanmasi.
/// </summary>
/// <remarks>
/// Bu dosyadaki SIRA davranisin kendisidir: ara katmanlarin yeri
/// degistiginde kod derlenmeye devam eder ama guvenlik ve loglama sessizce
/// bozulur. Her adimin neden orada durdugu satir satir yazili.
/// </remarks>
public static class IstekHattiKurulumu
{
    /// <summary>Ara katmanlari dogru sirayla baglar.</summary>
    public static WebApplication IstekHattiniKur(this WebApplication app)
    {
        // SIRA: en dista vekil basliklari. Sonraki her sey (loglama, hiz
        // sinirlamasi, kimlik) istemcinin gercek adresini gormeli.
        app.UseForwardedHeaders();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Istek loglamasi izleme kimliginden SONRA: boylece "GET /x 200" satiri
        // da ayni kimlikle etiketleniyor ve istegin ozeti ile ayrintisi bir arada
        // okunuyor.
        app.UseSerilogRequestLogging(secenekler =>
        {
            // Varsayilan sablon yolu ve sureyi yaziyor; kim ve nereden bilgisi
            // bir arizayi anlamak icin cogu zaman sart.
            secenekler.EnrichDiagnosticContext = (baglam, http) =>
            {
                baglam.Set("Ip", http.Connection.RemoteIpAddress?.ToString());

                if (http.User.Identity?.IsAuthenticated == true)
                    baglam.Set("KullaniciId", http.User.FindFirst(ClaimNames.Subject)?.Value);
            };

            // Saglik kontrolleri saniyede birkac kez geliyor; Information
            // seviyesinde yazilsalardi log'un tamamini kaplar ve gercek
            // istekleri gorunmez hâle getirirlerdi.
            secenekler.GetLevel = (http, sure, hata) =>
                hata is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(ApiKurulumu.WebCors);

        // Sira onemli: once "kimsin" (authentication), sonra "iznin var mi"
        // (authorization). Ters cevrilirse yetkilendirme heniz kimlik olusmadan calisir.
        app.UseAuthentication();
        app.UseAuthorization();

        // Hiz sinirlamasi kimlikten SONRA: bolutleme once kullaniciya, kimlik
        // yoksa IP'ye bakiyor ve kullaniciyi ancak kimlik dogrulandiktan sonra
        // bilebiliyoruz.
        app.UseRateLimiter();

        return app;
    }

    /// <summary>Saglik, sistem, zamanlanmis is, denetleyici ve arayuz uclarini baglar.</summary>
    public static WebApplication UclariBagla(this WebApplication app, IConfiguration configuration)
    {
        app.SaglikUclariniBagla();

        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", service = "Loca API" }))
           .WithName("Ping")
           .WithTags("Sistem");

        app.ZamanlanmisIsleriBagla(configuration);

        app.MapControllers();

        // Arayuz EN SONDA: geri dusum kurali kendinden onceki hicbir ucu
        // yutmamali. Once baglanmis olsaydi butun API yollari index.html
        // donerdi.
        app.ArayuzuServisEt();

        return app;
    }
}
