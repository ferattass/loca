namespace Loca.Domain.Enums;

/// <summary>
/// Bir refresh token'in neden iptal edildigi. Veritabaninda <c>int</c> olarak
/// saklanir; guvenlik incelemesinde "bu oturum neden kapandi" sorusunun
/// cevabi tek sorguyla alinabilsin diye kayit altina alinir.
/// </summary>
public enum RefreshTokenRevokeReason
{
    /// <summary>Normal yenileme. Yerine yeni token uretildi.</summary>
    Rotated = 1,

    /// <summary>Kullanici cikis yapti.</summary>
    LoggedOut = 2,

    /// <summary>Iptal edilmis token tekrar kullanildi — token calinmis olabilir.</summary>
    ReuseDetected = 3,

    /// <summary>Kullanici pasiflestirildi.</summary>
    UserDeactivated = 4,

    /// <summary>
    /// Sifre degistirildi veya sifirlandi. Eski sifreyle acilmis oturumlarin
    /// devam etmesi, sifreyi degistirmenin amacini bosa cikarirdi.
    /// </summary>
    PasswordChanged = 5,

    /// <summary>Kullanici token'i acikca iptal etti.</summary>
    RevokedByUser = 6
}
