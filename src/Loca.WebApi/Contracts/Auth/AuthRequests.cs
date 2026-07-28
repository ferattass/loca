namespace Loca.WebApi.Contracts.Auth;

/// <summary>
/// Istek govdeleri. Command siniflarindan ayri tutulur cunku command'lar
/// IP adresi gibi istemcinin gonderemeyecegi alanlar da tasir.
/// </summary>
/// <remarks>
/// IP dogrudan command'a alinsaydi istemci govdeye istedigi IP'yi yazabilirdi
/// ve guvenlik loglari yaniltici olurdu. IP baglantidan okunur.
/// </remarks>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);
