using Loca.Domain.Common;

namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Yurulukteki hizmet bedeli politikasi.
/// </summary>
/// <remarks>
/// <c>IReservationPolicy</c>'ye eklenmedi cunku o arayuz <c>IOptions</c>
/// uzerinden ACILISTA baglaniyor; hizmet bedeli ise panelden
/// degistirilebiliyor ve degisikligin uygulamayi yeniden baslatmadan
/// islemesi gerekiyor. Ayri arayuz, "hangisi calisma aninda okunuyor"
/// sorusunu tip seviyesinde cevapliyor.
/// </remarks>
public interface IServiceFeeProvider
{
    Task<ServiceFeePolicy> GetAsync(CancellationToken cancellationToken = default);
}
