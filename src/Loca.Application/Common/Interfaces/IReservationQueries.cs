using Loca.Application.Features.Reservations.Common;

namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Rezervasyon okuma tarafi. Yazma tarafindan ayri: projeksiyonla tek
/// <c>SELECT</c>, degisiklik takipcisine hicbir sey yazilmaz.
/// </summary>
public interface IReservationQueries
{
    /// <param name="utcNow">Kalan sureyi sunucu saatiyle hesaplamak icin.</param>
    Task<ReservationDetail?> GetDetailAsync(
        Guid id, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <remarks>
    /// Yeniden eskiye siralanir: kullanici en son yaptigi rezervasyonu
    /// aramak zorunda kalmasin.
    /// </remarks>
    Task<IReadOnlyList<ReservationListItem>> GetByUserAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
}
