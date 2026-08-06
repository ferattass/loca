using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;

namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Yonetim panelinin okuma tarafi.
/// </summary>
/// <remarks>
/// Panelin ihtiyaci olan sayilar bes ayri tablodan geliyor. Her biri kendi
/// repository'sinden cekilseydi tek bir ekran icin bes bagimlilik ve bes
/// ayri gidis donus gerekirdi; okuma tarafi zaten yazma tarafindan ayri
/// tutuluyor (bkz. <see cref="IReservationQueries"/>).
/// </remarks>
public interface IAdminQueries
{
    /// <param name="gunBasiUtc">Ozetin kapsadigi gunun baslangici.</param>
    Task<AdminOzeti> GetOverviewAsync(
        DateTime gunBasiUtc,
        DateTime utcNow,
        string activePaymentProvider,
        bool redisAvailable,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminOdemeSatiri>> GetPaymentsAsync(
        AdminOdemeFiltresi filtre, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminKullanici>> GetUsersAsync(
        AdminKullaniciFiltresi filtre, CancellationToken cancellationToken = default);

    /// <summary>Tek kullanicinin tum bilgisi ve son hareketleri.</summary>
    Task<AdminKullaniciDetayi?> GetUserDetailAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <param name="durum">
    /// <c>Pending</c>, <c>Retryable</c>, <c>DeadLettered</c> veya
    /// <c>Processed</c>.
    /// </param>
    Task<IReadOnlyList<KuyrukMesaji>> GetQueueAsync(
        string durum, int limit, CancellationToken cancellationToken = default);
}
