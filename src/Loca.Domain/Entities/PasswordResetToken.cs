using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Sifre sifirlama baglantisinin arkasindaki tek kullanimlik, sureli token.
/// </summary>
/// <remarks>
/// Token'in kendisi burada saklanmaz, yalnizca <see cref="TokenHash"/> tutulur.
/// Refresh token'lardan farkli davranilmasinin sebebi: bu token dogrudan sifre
/// degistirme yetkisi verir. Veritabani okumasi eline gecen biri (sizmis bir
/// yedek de dahil) duz token'lari gorseydi butun hesaplarin sifresini
/// degistirebilirdi. Ozetten token geri uretilemez.
///
/// <para>
/// Tek kullanimlik olmasi <see cref="UsedAt"/> ile saglanir: sifirlama
/// baglantisi e-posta kutusunda kalir, ikinci kez calismaz.
/// </para>
/// </remarks>
public sealed class PasswordResetToken : BaseEntity
{
    // EF Core materyalizasyon icin.
    private PasswordResetToken() => TokenHash = string.Empty;

    public PasswordResetToken(
        Guid userId, string tokenHash, DateTime expiresAt, string? requestedByIp = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Token bir kullaniciya bagli olmali.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Token ozeti bos olamaz.");

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        RequestedByIp = requestedByIp;
    }

    public Guid UserId { get; private set; }

    /// <summary>Token'in SHA-256 ozeti. Duz deger yalnizca kullaniciya gider.</summary>
    public string TokenHash { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Doldugu an token bir daha kullanilamaz.</summary>
    public DateTime? UsedAt { get; private set; }

    public string? RequestedByIp { get; private set; }

    public User? User { get; private set; }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsUsable(DateTime utcNow) => !IsUsed && !IsExpired(utcNow);

    public void MarkUsed(DateTime utcNow)
    {
        // Ilk kullanimin zamani korunur; ayni token ikinci kez geldiginde
        // zaten IsUsable false donuyor.
        if (IsUsed)
            return;

        UsedAt = utcNow;
    }
}
