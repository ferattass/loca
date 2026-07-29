using Loca.Application.Common.Models;

namespace Loca.Application.Features.Auth.Common;

/// <summary>
/// Kimlik dogrulama hatalari tek yerde toplanir.
/// </summary>
/// <remarks>
/// Mesajlar bilerek genel: "kullanici bulunamadi" ile "sifre yanlis" ayri
/// cevaplar verirse saldirgan hangi e-postalarin sistemde kayitli oldugunu
/// tek tek deneyerek ogrenebilir (kullanici sayimi / user enumeration).
/// </remarks>
internal static class AuthErrors
{
    internal static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "E-posta veya sifre hatali.");

    internal static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Auth.EmailAlreadyRegistered", "Bu e-posta adresi zaten kayitli.");

    internal static readonly Error AccountDisabled =
        Error.Forbidden("Auth.AccountDisabled", "Hesabiniz pasif durumda. Yonetici ile iletisime gecin.");

    internal static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "Oturum gecersiz. Lutfen tekrar giris yapin.");

    /// <remarks>
    /// "Token yok", "suresi dolmus" ve "zaten kullanilmis" ayni cevabi verir:
    /// ayrilsaydi elindeki degerin gecerli bir token olup olmadigi
    /// denenerek ogrenilebilirdi.
    /// </remarks>
    internal static readonly Error InvalidPasswordResetToken =
        Error.Unauthorized(
            "Auth.InvalidPasswordResetToken",
            "Sifirlama baglantisi gecersiz veya suresi dolmus. Yeni bir baglanti isteyin.");

    internal static readonly Error CurrentPasswordIncorrect =
        Error.Unauthorized("Auth.CurrentPasswordIncorrect", "Mevcut sifre hatali.");
}
