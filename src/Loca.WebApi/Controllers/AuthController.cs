using Loca.Application.Features.Auth.ChangePassword;
using Loca.Application.Features.Auth.Common;
using Loca.Application.Features.Auth.ForgotPassword;
using Loca.Application.Features.Auth.Login;
using Loca.Application.Features.Auth.Logout;
using Loca.Application.Features.Auth.Me;
using Loca.Application.Features.Auth.Refresh;
using Loca.Application.Features.Auth.Register;
using Loca.Application.Features.Auth.ResetPassword;
using Loca.Application.Features.Auth.RevokeToken;
using Loca.WebApi.Contracts.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Kimlik dogrulama uclari.
/// </summary>
/// <remarks>
/// Controller yalnizca cevirmendir: istegi bir command'a, <c>Result</c>'i
/// HTTP cevabina cevirir. Icinde tek bir is kurali yoktur — bu kural
/// architecture testleriyle korunuyor.
/// </remarks>
[Tags("Kimlik")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
    /// <summary>Yeni kullanici kaydi olusturur ve oturum acar.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FullName,
            request.PhoneNumber,
            ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>E-posta ve sifre ile oturum acar.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new LoginCommand(request.Email, request.Password, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Refresh token'i yenisiyle degistirir.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RefreshTokenCommand(request.RefreshToken, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Refresh token'i iptal ederek oturumu kapatir.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new LogoutCommand(request.RefreshToken, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Oturum acmis kullanicinin kendi refresh token'ini iptal eder.</summary>
    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken(
        [FromBody] RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RevokeTokenCommand(request.RefreshToken, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Mevcut sifreyi dogrulayarak yenisiyle degistirir.</summary>
    /// <remarks>Basarili oldugunda kullanicinin tum oturumlari kapanir.</remarks>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ChangePasswordCommand(
            request.CurrentPassword, request.NewPassword, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Sifre sifirlama baglantisi olusturur.</summary>
    /// <remarks>
    /// Adres kayitli olmasa da 204 doner: aksi hâlde hangi e-postalarin
    /// sistemde oldugu bu uc denenerek ogrenilebilirdi.
    /// </remarks>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ForgotPasswordCommand(request.Email, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Sifirlama token'i ile yeni sifre belirler.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ResetPasswordCommand(
            request.Token, request.NewPassword, ClientIpAddress);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Gecerli token'in sahibi olan kullaniciyi doner.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetCurrentUserQuery(), cancellationToken));

    /// <remarks>
    /// Istemcinin bildirdigi degere degil baglantinin kendisine bakilir.
    /// Ters vekil (reverse proxy) arkasinda calisirken X-Forwarded-For
    /// islenmesi icin ForwardedHeaders middleware'i gerekir — dagitim
    /// yapilandirmasi Gun 10'da eklenecek.
    /// </remarks>
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
}
