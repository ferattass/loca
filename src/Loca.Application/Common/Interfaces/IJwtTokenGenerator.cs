using Loca.Domain.Entities;

namespace Loca.Application.Common.Interfaces;

/// <summary>Uretilen token ve gecerlilik biti.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAt);

/// <inheritdoc cref="AccessToken"/>
public sealed record RefreshTokenValue(string Value, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    /// <summary>
    /// Kullanicinin kimligini ve rollerini tasiyan imzali access token uretir.
    /// </summary>
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> roles);

    /// <summary>
    /// Kriptografik olarak guvenli rastgele refresh token uretir.
    /// </summary>
    /// <remarks>
    /// <c>Guid.NewGuid()</c> kullanilmaz: Guid benzersizlik icin tasarlandi,
    /// tahmin edilemezlik icin degil. Refresh token tahmin edilirse baskasinin
    /// oturumu ele gecirilir.
    ///
    /// <para>
    /// Gecerlilik suresi de burada belirlenir; boylece token omurlerinin tamami
    /// tek yerde, yapilandirmadan okunur ve handler'lara sure bilgisi sizmaz.
    /// </para>
    /// </remarks>
    RefreshTokenValue CreateRefreshToken();
}
