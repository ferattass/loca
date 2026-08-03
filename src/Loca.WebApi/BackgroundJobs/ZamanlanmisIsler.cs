using Hangfire;
using Loca.Application.Features.Outbox.ProcessOutbox;
using Loca.Application.Features.Outbox.ScheduledReports;
using Loca.Application.Features.Reservations.ExpireReservations;
using Loca.Infrastructure.Concurrency;
using MediatR;
using Microsoft.Extensions.Options;

namespace Loca.WebApi.BackgroundJobs;

/// <summary>
/// Zamanlanmis islerin govdesi.
/// </summary>
/// <remarks>
/// Isler yalnizca TETIKLEYICI: is mantiginin tamami Application katmaninda
/// duran komutlarda. Boylece ayni is elle de calistirilabiliyor, testten de
/// cagrilabiliyor ve zamanlayici degistiginde (Hangfire'dan baskasina gecis)
/// mantiga dokunulmuyor.
///
/// <para>
/// <b>Metotlar public ve sinif somut:</b> Hangfire isi kuyruga yazarken tip
/// ve metot adini metin olarak sakliyor, calisma aninda yansimayla cagiriyor.
/// Arayuz uzerinden kaydedilseydi kuyrukta duran eski isler tip degisince
/// cozulemez hale gelirdi.
/// </para>
/// </remarks>
public sealed class ZamanlanmisIsler(ISender sender, IOptions<ReservationOptions> reservationOptions)
{
    private readonly ReservationOptions _reservationOptions = reservationOptions.Value;

    /// <summary>1 · Suresi dolan rezervasyonlari kapat, koltuklari birak.</summary>
    public Task SuresiDolanRezervasyonlariKapatAsync() =>
        sender.Send(new ExpireReservationsCommand(_reservationOptions.ExpiryBatchSize));

    /// <summary>2 · Bekleyen outbox mesajlarini gonder.</summary>
    public Task OutboxMesajlariniIsleAsync() =>
        sender.Send(new ProcessOutboxCommand(BatchSize: 100));

    /// <summary>
    /// 3 · Daha once basarisiz olmus mesajlari yeniden dene.
    /// </summary>
    /// <remarks>
    /// Ilk denemeden AYRI ve daha seyrek: surekli basarisiz olan bir mesaj
    /// her turda yeniden denenip yeni mesajlarin sirasini isgal etmesin.
    /// </remarks>
    public Task BasarisizOutboxMesajlariniYenidenDeneAsync() =>
        sender.Send(new ProcessOutboxCommand(BatchSize: 50, OnlyRetries: true));

    /// <summary>4 · Yaklasan etkinlikler icin hatirlatma kuyruga yaz.</summary>
    public Task YaklasanEtkinlikHatirlatmasiAsync() =>
        sender.Send(new SendUpcomingEventRemindersCommand());

    /// <summary>5 · Bir onceki gunun satis ozetini kuyruga yaz.</summary>
    public Task GunlukSatisOzetiAsync() =>
        sender.Send(new WriteDailySalesSummaryCommand());

    /// <summary>Deneme hakki tukenmis mesajlari raporla.</summary>
    public Task OluMektuplariRaporlaAsync() =>
        sender.Send(new ReportDeadLetteredOutboxCommand(BatchSize: 100));
}

public static class ZamanlanmisIsKaydi
{
    /// <summary>
    /// Tekrarlayan isleri kaydeder.
    /// </summary>
    /// <remarks>
    /// <b>Sabit is kimligi kullaniliyor.</b> <c>AddOrUpdate</c> ayni kimlikle
    /// cagrildiginda mevcut kaydi guncelliyor; kimlik olmasaydi her acilista
    /// yeni bir tekrarlayan is eklenir ve uygulama kac kez yeniden
    /// baslatildiysa is o kadar kez calisirdi.
    ///
    /// <para>
    /// Siklıklar isin maliyetine gore ayarli: koltuk birakma kullaniciyi
    /// dogrudan etkiledigi icin sik, gunluk rapor gunde bir kez.
    /// </para>
    /// </remarks>
    public static void TekrarlayanIsleriKaydet(IRecurringJobManager yonetici)
    {
        ArgumentNullException.ThrowIfNull(yonetici);

        // Koltugun serbest kalmasi kullaniciyi dogrudan etkiliyor: baskasi o
        // koltugu almak icin bekliyor olabilir. Bu yuzden en sik calisan is.
        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "rezervasyon-sure-dolumu",
            isler => isler.SuresiDolanRezervasyonlariKapatAsync(),
            Cron.Minutely);

        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "outbox-isle",
            isler => isler.OutboxMesajlariniIsleAsync(),
            Cron.Minutely);

        // Yeniden denemeler seyrek: hemen tekrarlamak, gecici olmayan bir
        // hatada yalnizca deneme hakkini hizlica tuketirdi.
        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "outbox-yeniden-dene",
            isler => isler.BasarisizOutboxMesajlariniYenidenDeneAsync(),
            "*/15 * * * *");

        // Saatte bir, bir saatlik pencereyi tariyor: is sikligi ile tarama
        // penceresi ayni olmali, yoksa ayni rezervasyona iki hatirlatma gider.
        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "yaklasan-etkinlik-hatirlatmasi",
            isler => isler.YaklasanEtkinlikHatirlatmasiAsync(),
            Cron.Hourly);

        // Gece 00:30 UTC: gun donumunden sonra, dunun tamamlanmis olmasi icin.
        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "gunluk-satis-ozeti",
            isler => isler.GunlukSatisOzetiAsync(),
            "30 0 * * *");

        yonetici.AddOrUpdate<ZamanlanmisIsler>(
            "olu-mektup-raporu",
            isler => isler.OluMektuplariRaporlaAsync(),
            Cron.Hourly);
    }
}
