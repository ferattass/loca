using Loca.Application.Common.Interfaces;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;

namespace Loca.Application.Features.Auth.Common;

/// <summary>
/// Access + refresh token ciftini uretir ve refresh token'i kaydeder.
/// </summary>
/// <remarks>
/// Kayit, giris ve yenileme akislarinin ucu de ayni isi yapiyor. Uc yere
/// kopyalansaydi ilerde token omru degistiginde biri unutulurdu.
///
/// <para>
/// <c>SaveChanges</c> burada CAGRILMAZ. Cagiran handler kendi degisiklikleriyle
/// (ornegin eski token'in iptali) birlikte tek seferde kaydeder; boylece yeni
/// token yazilip eskisi iptal edilmeden kalan bir ara durum olusmaz.
/// </para>
/// </remarks>
internal sealed class AuthTokenIssuer(
    IJwtTokenGenerator tokenGenerator,
    IRefreshTokenRepository refreshTokens)
{
    public AuthResponse Issue(User user, IReadOnlyCollection<string> roles, string? ipAddress)
    {
        ArgumentNullException.ThrowIfNull(user);

        var accessToken = tokenGenerator.CreateAccessToken(user, roles);
        var refreshToken = tokenGenerator.CreateRefreshToken();

        refreshTokens.Add(new RefreshToken(
            user.Id, refreshToken.Value, refreshToken.ExpiresAt, ipAddress));

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken.Value,
            new UserSummary(user.Id, user.Email, user.FullName, [.. roles]));
    }
}
