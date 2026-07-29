using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.RevokeToken;

/// <remarks>
/// Cikistan farki: <c>logout</c> elindeki token'i kapatir ve kimlik
/// dogrulamasi istemez. Bu uc ise oturum acmis kullanicinin kendi
/// cihazlarindan birini uzaktan dusurmesi icin — "telefonumu kaybettim"
/// durumu.
/// </remarks>
public sealed record RevokeTokenCommand(
    string RefreshToken,
    string? IpAddress) : IRequest<Result>;

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator() =>
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Refresh token zorunludur.");
}

internal sealed class RevokeTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser,
    ILogger<RevokeTokenCommandHandler> logger)
    : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(
        RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure(AuthErrors.InvalidCredentials);

        var stored = await refreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken);

        // Baskasinin token'i verildiginde de ayni cevap doner. "Bu token
        // baskasina ait" demek, denenen degerin gecerli bir token oldugunu
        // dogrulardi.
        if (stored is null || stored.UserId != userId)
        {
            logger.LogWarning(
                "Sahibi olmayan token icin iptal denemesi. KullaniciId: {KullaniciId}", userId);

            return Result.Failure(AuthErrors.InvalidRefreshToken);
        }

        if (!stored.IsRevoked)
        {
            stored.Revoke(
                dateTimeProvider.UtcNow,
                RefreshTokenRevokeReason.RevokedByUser,
                revokedByIp: request.IpAddress);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Token iptal edildi. KullaniciId: {KullaniciId}", userId);
        }

        return Result.Success();
    }
}
