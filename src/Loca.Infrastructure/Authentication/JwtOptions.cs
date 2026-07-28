using System.ComponentModel.DataAnnotations;

namespace Loca.Infrastructure.Authentication;

/// <summary>
/// Token ayarlari. <c>Jwt</c> bolumunden okunur.
/// </summary>
/// <remarks>
/// Sureler koda gomulmez: testte access token'in suresi 1 saniyeye indirilip
/// yenileme akisi beklemeden dogrulanabilsin.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Imzalama anahtari. Depoya girmez; gelistirmede user-secrets,
    /// konteynerde <c>Jwt__Secret</c> ortam degiskeni uzerinden gelir.
    /// </summary>
    /// <remarks>
    /// HMAC-SHA256 icin anahtarin en az 256 bit (32 karakter) olmasi gerekir.
    /// Kisa anahtar calisma aninda istisna firlatir; bu yuzden uzunluk
    /// baslangicta dogrulanir, ilk giris denemesinde degil.
    /// </remarks>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Secret en az 32 karakter olmali (HMAC-SHA256 icin 256 bit).")]
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Loca";

    public string Audience { get; set; } = "LocaClient";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;
}
