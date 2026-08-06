using Loca.Application.Common.Interfaces;
using Loca.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Loca.WebApi.HealthChecks;

/// <summary>
/// Veritabanina gercekten sorgu atabiliyor muyuz.
/// </summary>
/// <remarks>
/// Agir bir sorgu degil, yalnizca baglanti denemesi: bu uc yuk
/// dengeleyici tarafindan saniyede birkac kez cagriliyor ve gercek bir
/// sorgu, saglik kontrolunun kendisini yuk kaynagina cevirirdi.
/// </remarks>
internal sealed class VeritabaniSaglikKontrolu(LocaDbContext veritabani) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ayakta = await veritabani.Database.CanConnectAsync(cancellationToken);

            return ayakta
                ? HealthCheckResult.Healthy("Veritabanina baglaniliyor.")
                : HealthCheckResult.Unhealthy("Veritabanina baglanilamiyor.");
        }
        catch (Exception hata) when (hata is not OperationCanceledException)
        {
            // Istisna metni DISARI VERILMIYOR: baglanti dizesi ve sunucu
            // adi bu metinde gecebiliyor ve saglik ucu kimlik dogrulamasiz.
            return HealthCheckResult.Unhealthy("Veritabanina baglanilamiyor.");
        }
    }
}

/// <summary>
/// Redis ayakta mi.
/// </summary>
/// <remarks>
/// <b>Sonuc bilerek <c>Degraded</c>, <c>Unhealthy</c> degil.</b> Redis
/// koltuk kilidinde yalnizca hizli on eleme; kapali oldugunda sistem
/// dogru calismaya devam ediyor, sadece yavasliyor (olculdu: 212 ms →
/// 3058 ms). Unhealthy dondurseydik yuk dengeleyici calisan bir sunucuyu
/// havuzdan cikarir ve Redis arizasi tam bir kesintiye donusurdu.
/// </remarks>
internal sealed class RedisSaglikKontrolu(IDistributedLockService kilitler) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var ayakta = await kilitler.IsAvailableAsync(cancellationToken);

        return ayakta
            ? HealthCheckResult.Healthy("Redis ayakta.")
            : HealthCheckResult.Degraded(
                "Redis kapali. Koltuk kilidi veritabani tarafinda korunuyor, akis yavas.");
    }
}
