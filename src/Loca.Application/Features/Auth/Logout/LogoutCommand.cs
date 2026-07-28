using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.Logout;

public sealed record LogoutCommand(
    string RefreshToken,
    string? IpAddress) : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() =>
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Refresh token zorunludur.");
}

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<LogoutCommandHandler> logger)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (stored is not null && !stored.IsRevoked)
        {
            stored.Revoke(
                dateTimeProvider.UtcNow,
                RefreshTokenRevokeReason.LoggedOut,
                revokedByIp: request.IpAddress);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Cikis yapildi. KullaniciId: {KullaniciId}", stored.UserId);
        }

        // Token bulunamasa da basarili donulur. "Boyle bir token yok" cevabi,
        // elindeki degerin gecerli olup olmadigini saldirgana soylerdi.
        // Ayrica cikis islemi kullanicinin gozunde her hâlukârda basarilidir.
        return Result.Success();
    }
}
