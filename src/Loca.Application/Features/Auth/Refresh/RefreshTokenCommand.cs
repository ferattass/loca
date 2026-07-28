using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress) : IRequest<Result<AuthResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() =>
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Refresh token zorunludur.");
}

/// <summary>
/// Refresh token'i yenisiyle degistirir (rotation) ve yeniden kullanimi yakalar.
/// </summary>
/// <remarks>
/// Rotation'in mantigi: her yenilemede eski token iptal edilir ve yenisi verilir.
/// Boylece bir token yalnizca bir kez kullanilabilir.
///
/// <para>
/// Yeniden kullanim tespiti bunun uzerine kurulur: iptal edilmis bir token
/// tekrar geldiyse iki ihtimal var — ya token calindi ve saldirgan kullaniyor,
/// ya da kurban zaten yenilemis ve saldirganin elinde eski kopya kaldi. Iki
/// durumda da dogru davranis ayni: o kullanicinin TUM oturumlarini kapatmak.
/// </para>
/// </remarks>
internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    AuthTokenIssuer tokenIssuer,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (stored is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var now = dateTimeProvider.UtcNow;

        if (stored.IsRevoked)
        {
            await RevokeAllSessionsAsync(stored.UserId, now, request.IpAddress, cancellationToken);

            logger.LogWarning(
                "Iptal edilmis refresh token tekrar kullanildi, kullanicinin tum oturumlari kapatildi. " +
                "KullaniciId: {KullaniciId}, IlkIptalSebebi: {Sebep}",
                stored.UserId,
                stored.RevokeReason);

            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);
        }

        if (stored.IsExpired(now))
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var user = stored.User ?? await users.GetByIdAsync(stored.UserId, cancellationToken);

        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        if (!user.IsActive)
        {
            // Hesap pasiflestirildiyse elindeki token'la devam edememeli.
            await RevokeAllSessionsAsync(user.Id, now, request.IpAddress, cancellationToken);
            return Result.Failure<AuthResponse>(AuthErrors.AccountDisabled);
        }

        var roles = user.UserRoles
            .Where(userRole => userRole.Role is not null)
            .Select(userRole => userRole.Role!.Name)
            .ToList();

        var response = tokenIssuer.Issue(user, roles, request.IpAddress);

        // Eski token yenisine baglanarak iptal edilir. ReplacedByToken zinciri
        // sayesinde bir sizinti incelemesinde oturumun gecmisi takip edilebilir.
        stored.Revoke(now, RefreshTokenRevokeReason.Rotated, response.RefreshToken, request.IpAddress);

        // Tek SaveChanges: yeni token'in yazilmasi ve eskisinin iptali
        // ayni islemde gerceklesir, arada ikisinin de gecerli oldugu an olmaz.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }

    private async Task RevokeAllSessionsAsync(
        Guid userId, DateTime now, string? ipAddress, CancellationToken cancellationToken)
    {
        var activeTokens = await refreshTokens.GetActiveByUserIdAsync(userId, cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke(now, RefreshTokenRevokeReason.ReuseDetected, revokedByIp: ipAddress);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
