namespace Loca.WebApi.Contracts.Admin;

/// <param name="Grant"><c>true</c> rolu verir, <c>false</c> geri alir.</param>
/// <remarks>
/// Rol adi metin olarak tasiniyor ama serbest degil: dogrulayici sistemdeki
/// uc rolden biri olmasini zorunlu kiliyor. Yazim hatasi olan bir rol
/// veritabanina yazilsaydi hicbir yetki kontrolune takilmadigi icin
/// sessizce etkisiz kalirdi.
/// </remarks>
public sealed record ChangeUserRoleRequest(string RoleName, bool Grant);
