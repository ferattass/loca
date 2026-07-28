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

    bool IsAuthenticated { get; }

    bool IsInRole(string roleName);
}
