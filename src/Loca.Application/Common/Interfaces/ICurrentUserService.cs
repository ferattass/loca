namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Istegi yapan kullanicinin kimligi. HTTP baglamindan okunur ama
/// Application katmani HTTP'yi tanimadigi icin arayuz uzerinden verilir.
/// </summary>
/// <remarks>
/// Iki yerde kullanilir: audit alanlarinin doldurulmasi (<c>CreatedBy</c>)
/// ve kaynak sahipligi kontrolu (kendi etkinligini duzenleme).
/// </remarks>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Email { get; }

    /// <summary>
    /// Istegin geldigi adres. Arka plan islerinde ve okunamadigi durumda
    /// <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Odeme saglayicisi dolandiricilik puanlamasi icin bu adresi istiyor.
    /// Token'dan degil baglantidan okunuyor: istemcinin gonderdigi bir
    /// degere guvenilseydi, adres istegin kendisi tarafindan uydurulabilir
    /// ve puanlama anlamsizlasirdi.
    /// </remarks>
    string? IpAddress { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string roleName);
}
