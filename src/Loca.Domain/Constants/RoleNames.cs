namespace Loca.Domain.Constants;

/// <summary>
/// Rol adlari. Sabit olarak tutulur cunku bu metinler
/// <c>[Authorize(Roles = ...)]</c> icinde ve policy tanimlarinda gecer;
/// yazim hatasi derleme zamaninda degil calisma aninda 403 olarak ortaya cikar.
/// </summary>
/// <remarks>
/// Analiz belgesindeki karsiliklari: Customer = Kullanici,
/// Organizer = Organizator, Admin = Admin. Bir kullanici birden fazla
/// role sahip olabilir (organizator ayni zamanda bilet alabilir).
/// </remarks>
public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Organizer = "Organizer";

    /// <summary>
    /// Basvuru ve etkinlik onaylayan ekip.
    /// </summary>
    /// <remarks>
    /// <b>Admin'in kisitlanmis hâli.</b> Onay isi buyudukce tek bir admin
    /// hesabiyla yurumuyor, ama onay verecek herkese admin yetkisi vermek
    /// odeme ayarlarini, kullanici rollerini ve sirlari da acmak demek
    /// olurdu. Moderator yalnizca onay kuyrugunu ve etkinlikleri goruyor.
    /// </remarks>
    public const string Moderator = "Moderator";

    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Organizer, Moderator, Admin];
}
