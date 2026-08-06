namespace Loca.WebApi.Authorization;

/// <summary>
/// Hiz sinirlamasi politika adlari.
/// </summary>
/// <remarks>
/// Policy adlari metin oldugu icin yazim hatasi derleme zamaninda
/// yakalanmiyor; <c>[EnableRateLimiting("ath")]</c> yazilsaydi uygulama
/// calisirken istisna firlatirdi. Sabitler bu hatayi derleyiciye
/// tasiyor.
///
/// <para>
/// Neden ayri politikalar: uclarin maliyeti ayni degil. Bir giris
/// denemesi yalnizca CPU harciyor, bir sifre sifirlama istegi ise
/// <b>baskasinin posta kutusuna</b> mesaj gonderiyor. Ikisine ayni
/// esigi vermek, ya girisi gereksiz dar ya sifirlamayi tehlikeli genis
/// birakirdi.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Giris, kayit, token yenileme.</summary>
    public const string Auth = "auth";

    /// <summary>Sifre sifirlama istegi — her cagri bir e-posta gonderiyor.</summary>
    public const string PasswordReset = "password-reset";
}
