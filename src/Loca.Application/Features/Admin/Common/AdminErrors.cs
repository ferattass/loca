using Loca.Application.Common.Models;

namespace Loca.Application.Features.Admin.Common;

public static class AdminErrors
{
    public static readonly Error QueueMessageNotFound =
        Error.NotFound("Admin.QueueMessageNotFound", "Kuyruk mesaji bulunamadi.");

    /// <remarks>
    /// Zaten islenmis veya hakki bitmemis bir mesaji geri koymak, kuyrugun
    /// sirasini bozmaktan baska bir ise yaramaz.
    /// </remarks>
    public static readonly Error QueueMessageNotDeadLettered =
        Error.Conflict(
            "Admin.QueueMessageNotDeadLettered",
            "Yalnizca deneme hakki tukenmis mesaj kuyruga geri konabilir.");

    public static readonly Error UserNotFound =
        Error.NotFound("Admin.UserNotFound", "Kullanici bulunamadi.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Admin.RoleNotFound", "Rol bulunamadi.");

    /// <remarks>
    /// Admin kendi admin rolunu alamaz. Alabilseydi tek adminli bir sistemde
    /// panele girisi olan hic kimse kalmayabilirdi ve geri donusu yalnizca
    /// veritabanina elle mudahaleyle mumkun olurdu.
    /// </remarks>
    public static readonly Error CannotRemoveOwnAdminRole =
        Error.Conflict(
            "Admin.CannotRemoveOwnAdminRole",
            "Kendi admin rolunuzu kaldiramazsiniz.");

    public static readonly Error SettingNotFound =
        Error.NotFound("Admin.SettingNotFound", "Boyle bir ayar yok.");

    public static readonly Error SettingValueInvalid =
        Error.Validation("Admin.SettingValueInvalid", "Ayar degeri gecersiz.");
}
