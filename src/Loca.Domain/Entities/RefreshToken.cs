using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Access token suresi doldugunda yenisini almayi saglayan uzun omurlu token.
/// </summary>
/// <remarks>
/// Access token 15 dakika, refresh token 7 gun yasar. Kisa access suresi
/// calinan token'in omrunu sinirlar; refresh bunu kullaniciya hissettirmeden
/// telafi eder.
///
/// <para>
/// Token her kullanimda degistirilir (rotation) ve eskisi
/// <see cref="ReplacedByToken"/> ile yenisine baglanir. Boylece iptal edilmis
/// bir token tekrar geldiginde zincir takip edilebilir: bu, token'in
/// calindigina dair en net isarettir.
/// </para>
/// </remarks>
public sealed class RefreshToken : BaseEntity
{
    // EF Core materyalizasyon icin.
    private RefreshToken() => Token = string.Empty;

    public RefreshToken(Guid userId, string token, DateTime expiresAt, string? createdByIp = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("Token bos olamaz.");

        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public Guid UserId { get; private set; }

    /// <summary>Kriptografik olarak rastgele uretilen deger. UNIQUE index'lidir.</summary>
    public string Token { get; private set; }

    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }

    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public RefreshTokenRevokeReason? RevokeReason { get; private set; }

    public User? User { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    /// <summary>Kullanilabilir mi: ne iptal edilmis ne de suresi dolmus.</summary>
    public bool IsUsable(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(
        DateTime utcNow,
        RefreshTokenRevokeReason reason,
        string? replacedByToken = null,
        string? revokedByIp = null)
    {
        // Zaten iptal edilmisse ilk iptalin sebebi ve zamani korunur.
        // Yeniden kullanim tespitinde kullanicinin TUM token'lari iptal edilir;
        // bu tarama sirasinda daha once iptal edilmis olanlara dokunulmamali.
        if (IsRevoked)
            return;

        RevokedAt = utcNow;
        RevokeReason = reason;
        ReplacedByToken = replacedByToken;
        RevokedByIp = revokedByIp;
    }
}
