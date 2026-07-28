namespace Loca.Application.Common.Authentication;

/// <summary>
/// Token icindeki claim adlari.
/// </summary>
/// <remarks>
/// Token'i ureten (Infrastructure) ile dogrulayan (WebApi) tarafin ayni
/// adlari kullanmasi zorunlu. Iki yerde ayri metin yazilsaydi token uretilir,
/// dogrulanir ama rol eslesmedigi icin her istek 403 donerdi — ve bu hata
/// derleme zamaninda hic gorunmezdi.
///
/// <para>
/// <c>sub</c>, <c>email</c> ve <c>name</c> RFC 7519'da tanimli standart
/// adlardir. <c>role</c> standart degildir; ASP.NET Core'un varsayilan uzun
/// URI'li rol claim'i yerine kisa ad tercih edildi — token boyutu kucuk kalsin.
/// </para>
/// </remarks>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
