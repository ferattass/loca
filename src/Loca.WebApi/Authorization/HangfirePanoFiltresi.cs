using Hangfire.Dashboard;
using Loca.Domain.Constants;

namespace Loca.WebApi.Authorization;

/// <summary>
/// Hangfire panosunu yalnizca Admin rolune acar.
/// </summary>
/// <remarks>
/// <b>Pano kimlik dogrulamasi yapmaz, VAR OLAN kimligi okur.</b> Hangfire
/// kendi giris ekranini sunmuyor; buraya gelen istekte bir kimlik yoksa
/// kullanici disarida kaliyor. Panonun kendisi bir arayuz oldugu ve
/// tarayici JWT'yi kendiliginden gondermedigi icin pratikte pano yalnizca
/// aynı tarayicida kimlik dogrulanmis bir oturum varken aciliyor.
///
/// <para>
/// Gun 7'ye kadar pano yalnizca gelistirmede aciktı ve uretimde tamamen
/// kapaliydi. Kapali birakmak guvenliydi ama isleri gormenin tek yolunu da
/// kapatiyordu: uretimde bir is takildiginda elde yalnizca outbox tablosu
/// kaliyordu. Filtre, panoyu kapatmadan acmanin yolu.
/// </para>
///
/// <para>
/// Pano is govdelerini ve hata ayrintilarini gosteriyor; bu govdeler
/// kisisel veri tasiyor. Bu yuzden erisim Organizer'a bile acilmadi,
/// yalnizca Admin.
/// </para>
/// </remarks>
internal sealed class HangfirePanoFiltresi : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var kullanici = context.GetHttpContext().User;

        return kullanici.Identity?.IsAuthenticated == true
            && kullanici.IsInRole(RoleNames.Admin);
    }
}
