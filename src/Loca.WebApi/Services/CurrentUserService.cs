using System.Security.Claims;
using Loca.Application.Common.Authentication;
using Loca.Application.Common.Interfaces;

namespace Loca.WebApi.Services;

/// <summary>
/// Istegi yapan kullaniciyi token claim'lerinden okur.
/// </summary>
/// <remarks>
/// Uygulamasi WebApi'de cunku kaynagi <c>HttpContext</c>. Application katmani
/// yalnizca <see cref="ICurrentUserService"/> arayuzunu tanir; boylece ayni
/// handler bir arka plan isinden cagrildiginda farkli bir uygulama verilebilir.
/// </remarks>
internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    public Guid? UserId =>
        Guid.TryParse(FindClaim(ClaimNames.Subject), out var userId) ? userId : null;

    public string? Email => FindClaim(ClaimNames.Email);

    /// <remarks>
    /// <c>X-Forwarded-For</c> basligi OKUNMUYOR. Vekil sunucu arkasinda
    /// calisirken gercek adresi o baslik tasir ama basligi istemci de
    /// gonderebilir; dogrudan okunmasi adresi uydurulabilir yapardi. Vekil
    /// arkasina alindiginda dogru cozum <c>ForwardedHeaders</c> ara
    /// yazilimini guvenilen vekil listesiyle yapilandirmak; o zaman
    /// <c>RemoteIpAddress</c> zaten dogru degeri gosterir.
    /// </remarks>
    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string roleName) =>
        httpContextAccessor.HttpContext?.User.IsInRole(roleName) ?? false;

    private string? FindClaim(string claimType) =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
}
