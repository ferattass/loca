namespace Loca.Application.Features.Auth.Common;

/// <summary>
/// Basarili kimlik dogrulama cevabi.
/// </summary>
/// <remarks>
/// Entity degil DTO doner: <c>User</c> icinde <c>PasswordHash</c> var ve
/// veritabani semasi API sozlesmesine donusmemeli. Bu kural architecture
/// testi #5 ile korunuyor.
/// </remarks>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    UserSummary User);

public sealed record UserSummary(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);
