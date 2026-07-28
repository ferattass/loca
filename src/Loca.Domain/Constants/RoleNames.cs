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
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Organizer, Admin];
}
