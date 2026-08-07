namespace Loca.WebApi.Contracts.Admin;

/// <param name="Grant"><c>true</c> rolu verir, <c>false</c> geri alir.</param>
/// <remarks>
/// Rol adi metin olarak tasiniyor ama serbest degil: dogrulayici sistemdeki
/// uc rolden biri olmasini zorunlu kiliyor. Yazim hatasi olan bir rol
/// veritabanina yazilsaydi hicbir yetki kontrolune takilmadigi icin
/// sessizce etkisiz kalirdi.
/// </remarks>
public sealed record ChangeUserRoleRequest(string RoleName, bool Grant);

/// <param name="Roles">
/// Verilecek roller. <c>Admin</c> kabul edilmiyor: hesap acma ve yetki
/// yukseltme ayri isler, ikisi tek istekte yapilabilseydi panele erisen
/// biri kendi kontrol ettigi bir adrese admin hesabi acabilirdi.
/// </param>
public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles);
