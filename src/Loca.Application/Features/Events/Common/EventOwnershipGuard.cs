using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Domain.Constants;
using Loca.Domain.Entities;

namespace Loca.Application.Features.Events.Common;

/// <summary>
/// Etkinlik uzerindeki sahiplik kontrolunu handler'lar icin tek satira indirir.
/// </summary>
/// <remarks>
/// Yedi handler ayni uc satiri (giris yapilmis mi, admin mi, sahibi mi)
/// tekrar yazacakti. Bir tanesinde admin kontrolu atlanirsa admin kendi
/// olmayan etkinligi yonetemez hâle gelir — ve bu, hata vermeyen bir
/// davranis oldugu icin ancak biri sikayet edince fark edilir.
/// </remarks>
internal static class EventOwnershipGuard
{
    /// <returns>
    /// Erisim varsa <c>null</c>; yoksa handler'in dogrudan dondurebilecegi hata.
    /// </returns>
    public static Error? Check(ICurrentUserService currentUser, Event ev)
    {
        if (currentUser.UserId is null)
            return EventErrors.Unauthenticated;

        return Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), ev)
            ? null
            : EventErrors.NotOwner;
    }
}
