using Hangfire;
using Hangfire.PostgreSql;
using Loca.WebApi.Authorization;
using Loca.WebApi.BackgroundJobs;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Zamanlanmis islerin (Hangfire) kurulumu.
/// </summary>
/// <remarks>
/// Gun 6'da basit bir BackgroundService vardi; sure dolumu yalnizca uygulama
/// ayaktayken calisiyordu ve bir turun basarisiz olup olmadigi gorunmuyordu.
/// Hangfire isleri veritabaninda tutuyor: uygulama yeniden baslasa da kaldigi
/// yerden devam ediyor, basarisiz is yeniden deneniyor ve panelden
/// gozlemlenebiliyor. Isin kendisi Application katmaninda durdugu icin bu
/// gecis yalnizca tetikleyiciyi degistirdi.
/// </remarks>
public static class ZamanlanmisIsKurulumu
{
    /// <summary>
    /// Sure dolumu turunun sikligi.
    /// </summary>
    /// <remarks>
    /// Bu deger Gun 7'den beri OLU idi: is Hangfire'a tasininca sabit
    /// Cron.Minutely yazilmis, ayar appsettings'te ve kabul betiklerinin
    /// belgesinde durmaya devam etmisti.
    ///
    /// <para>
    /// Hem Hangfire sunucusunun yoklama araligi hem de tekrarlayan isin
    /// cron'u ayni degeri okumak zorunda; tek yerden veriliyor ki ikisi
    /// birbirinden ayrisamasin.
    /// </para>
    /// </remarks>
    public static int SureDolumuSaniyesi(IConfiguration configuration) =>
        configuration.GetValue<int?>("Reservation:ExpirySweepSeconds") ?? 30;

    /// <summary>Hangfire deposunu, sunucusunu ve is govdelerini kaydeder.</summary>
    public static IServiceCollection ZamanlanmisIsleriEkle(
        this IServiceCollection services, IConfiguration configuration)
    {
        var hangfireBaglanti = configuration.GetConnectionString("Default")!;

        services.AddHangfire(yapilandirma => yapilandirma
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(secenekler => secenekler.UseNpgsqlConnection(hangfireBaglanti)));

        var sureDolumuSaniye = SureDolumuSaniyesi(configuration);

        // Islerin cogu veritabani islemi; is parcacigi sayisi cekirdek sayisiyla
        // sinirlaniyor, varsayilan (cekirdek x 5) baglanti havuzunu tuketebilir.
        services.AddHangfireServer(secenekler =>
        {
            secenekler.WorkerCount = Math.Max(2, Environment.ProcessorCount);

            // Cron tek basina yetmiyor: Hangfire zamanlanmis isleri kendi yoklama
            // araliginda ariyor ve varsayilan aralik 15 saniye. Bes saniyelik bir
            // cron yazilip buraya dokunulmasaydi is yine on bes saniyede bir
            // kosardi — ayar gorunurde isler, gercekte islemezdi.
            if (sureDolumuSaniye is > 0 and < 15)
                secenekler.SchedulePollingInterval = TimeSpan.FromSeconds(sureDolumuSaniye);
        });

        services.AddScoped<ZamanlanmisIsler>();

        return services;
    }

    /// <summary>Hangfire panosunu baglar ve tekrarlayan isleri yazar.</summary>
    public static WebApplication ZamanlanmisIsleriBagla(
        this WebApplication app, IConfiguration configuration)
    {
        // Hangfire panosu artik uretimde de acik ama YALNIZCA Admin rolune.
        // Kapali birakmak guvenliydi, isleri gormenin tek yolunu da kapatiyordu:
        // uretimde bir is takildiginda elde yalnizca outbox tablosu kalirdi.
        // Filtre olmadan acilsaydi is govdeleri (kisisel veri tasiyorlar) ve hata
        // ayrintilari kimlik dogrulamasi olmadan gorulurdu.
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfirePanoFiltresi()]
        });

        // Tekrarlayan isler her acilista AddOrUpdate ile yeniden yaziliyor: sabit
        // kimlik kullanildigi icin kopyalanmiyor, yalnizca guncelleniyor. Kodda
        // degisen bir siklik boylece deploy ile birlikte etkili oluyor.
        ZamanlanmisIsKaydi.TekrarlayanIsleriKaydet(
            app.Services.GetRequiredService<IRecurringJobManager>(),
            SureDolumuSaniyesi(configuration));

        return app;
    }
}
