using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Loca.Application.Common.Authentication;
using Loca.Application.Common.Interfaces;
using Loca.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Loca.Infrastructure.Authentication;

internal sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider) : IJwtTokenGenerator
{
    /// <summary>Refresh token'in bayt uzunlugu. 32 bayt = 256 bit entropi.</summary>
    private const int RefreshTokenBytes = 32;

    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.Name, user.FullName),

            // Her token'a benzersiz kimlik: ilerde tek bir token'i kara listeye
            // almak gerekirse referans noktasi olur.
            new(ClaimNames.TokenId, Guid.CreateVersion7().ToString())
        };

        // Rol basina ayri claim. Tek claim'e virgulle yazilsaydi
        // [Authorize(Roles = "Admin")] eslesmesi calismazdi.
        claims.AddRange(roles.Select(role => new Claim(ClaimNames.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        // Kriptografik rastgelelik. Random veya Guid tahmin edilebilir
        // orunturler uretebilir; refresh token tahmin edilirse oturum calinir.
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenBytes);

        // Base64Url: token URL ve baslikta sorunsuz tasinsin diye
        // +, / ve = karakterleri bulunmaz.
        var value = Base64UrlEncoder.Encode(bytes);

        return new RefreshTokenValue(
            value,
            dateTimeProvider.UtcNow.AddDays(_options.RefreshTokenDays));
    }
}
