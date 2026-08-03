using Loca.Application.Features.Reservations.ExpireReservations;
using Loca.Infrastructure.Concurrency;
using MediatR;
using Microsoft.Extensions.Options;

namespace Loca.WebApi.BackgroundServices;

/// <summary>
/// Suresi dolan rezervasyonlari belirli araliklarla toplar.
/// </summary>
/// <remarks>
/// Yalnizca ZAMANLAYICI; isin kendisi
/// <see cref="ExpireReservationsCommand"/> icinde. Boylece ayni is yarin
/// Hangfire'a tasindiginda tek satir degisecek.
///
/// <para>
/// <b>Kendi kapsamini acar.</b> <c>BackgroundService</c> singleton olarak
/// calisir, <c>DbContext</c> ise istek omurlu (scoped). Singleton bir
/// servise dogrudan enjekte edilseydi tum uygulama omru boyunca TEK bir
/// <c>DbContext</c> kullanilirdi: degisiklik takipcisi surekli buyur,
/// bellek sizar ve bir turda olusan hata sonraki turlari da bozardi.
/// </para>
/// </remarks>
internal sealed class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationOptions> options,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ayarlar = options.Value;
        var aralik = TimeSpan.FromSeconds(ayarlar.ExpirySweepSeconds);

        logger.LogInformation(
            "Rezervasyon sure dolumu servisi basladi. Aralik: {Aralik} sn, Grup: {Grup}",
            ayarlar.ExpirySweepSeconds,
            ayarlar.ExpiryBatchSize);

        using var timer = new PeriodicTimer(aralik);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                await sender.Send(new ExpireReservationsCommand(ayarlar.ExpiryBatchSize), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapaniyor; normal cikis.
                break;
            }
            catch (Exception exception)
            {
                // TUR BASINA YAKALA. Yakalanmazsa tek bir hatali tur
                // dongunun tamamini sonlandirir ve sure dolumu uygulama
                // yeniden baslatilana kadar hic calismaz — koltuklar
                // kalici olarak kilitli gorunurdu.
                logger.LogError(exception, "Rezervasyon sure dolumu turu basarisiz oldu.");
            }
        }

        logger.LogInformation("Rezervasyon sure dolumu servisi durdu.");
    }
}
