using Loca.WebApi.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Saglik kontrollerinin kurulumu ve uclarinin baglanmasi.
/// </summary>
/// <remarks>
/// Iki ayri soru iki ayri uc: "surec ayakta mi" (liveness) ve "istek
/// alabilir mi" (readiness). Tek uc olsaydi, veritabani birkac saniye
/// yanit vermediginde yonlendirici surecin kendisini olu sayip yeniden
/// baslatirdi — oysa yapilmasi gereken tek sey trafigi kesmek.
/// </remarks>
public static class SaglikKurulumu
{
    /// <summary>Veritabani ve Redis kontrollerini kaydeder.</summary>
    public static IServiceCollection SaglikKontrolleriEkle(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<VeritabaniSaglikKontrolu>("veritabani", tags: ["hazir"])
            .AddCheck<RedisSaglikKontrolu>("redis", tags: ["hazir"]);

        return services;
    }

    /// <summary>Liveness ve readiness uclarini baglar.</summary>
    public static WebApplication SaglikUclariniBagla(this WebApplication app)
    {
        // Surec ayakta mi — bagimliliklara BAKMIYOR. Buraya bir veritabani
        // kontrolu konsaydi, veritabani birkac saniye yanit vermediginde
        // yonlendirici surecin kendisini olu sayip yeniden baslatirdi.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        // Istek alabilir mi — bagimliliklara BAKIYOR.
        app.MapHealthChecks("/health/hazir", new HealthCheckOptions
        {
            Predicate = kontrol => kontrol.Tags.Contains("hazir"),
            ResponseWriter = async (context, rapor) =>
            {
                context.Response.ContentType = "application/json";

                // Yalnizca ad ve durum. Istisna metni ve sure DISARI VERILMIYOR:
                // bu uc kimlik dogrulamasiz ve hata metinleri baglanti dizesi,
                // sunucu adi gibi seyler tasiyabiliyor.
                var govde = new
                {
                    durum = rapor.Status.ToString(),
                    kontroller = rapor.Entries.Select(g => new
                    {
                        ad = g.Key,
                        durum = g.Value.Status.ToString()
                    })
                };

                await context.Response.WriteAsJsonAsync(govde);
            }
        });

        return app;
    }
}
