using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string? IpAddress) : IRequest<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty().WithMessage("Token zorunludur.");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("Yeni sifre zorunludur.")
            .MinimumLength(8).WithMessage("Sifre en az 8 karakter olmali.")
            .MaximumLength(128).WithMessage("Sifre en fazla 128 karakter olabilir.")
            .Matches("[A-Za-z]").WithMessage("Sifre en az bir harf icermeli.")
            .Matches("[0-9]").WithMessage("Sifre en az bir rakam icermeli.");
    }
}

internal sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository resetTokens,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IPasswordResetTokenGenerator tokenGenerator,
    IDateTimeProvider dateTimeProvider,
    ILogger<ResetPasswordCommandHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Veritabaninda ozet duruyor; gelen duz token ayni fonksiyondan
        // gecirilip oyle araniyor.
        var hash = tokenGenerator.Hash(request.Token);
        var stored = await resetTokens.GetByHashAsync(hash, cancellationToken);

        var now = dateTimeProvider.UtcNow;

        if (stored is null || !stored.IsUsable(now) || stored.User is null)
        {
            logger.LogWarning("Gecersiz sifre sifirlama token'i kullanildi.");
            return Result.Failure(AuthErrors.InvalidPasswordResetToken);
        }

        var user = stored.User;

        if (!user.IsActive)
            return Result.Failure(AuthErrors.AccountDisabled);

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));

        // Tek kullanimlik: ayni baglanti ikinci kez calismaz.
        stored.MarkUsed(now);

        // Sifirlama genelde "hesabima erisemiyorum" durumunda yapilir; hesap
        // baskasinin elindeyse onun acik oturumlari da kapanmali.
        var active = await refreshTokens.GetActiveByUserIdAsync(user.Id, cancellationToken);
        foreach (var token in active)
            token.Revoke(now, RefreshTokenRevokeReason.PasswordChanged, revokedByIp: request.IpAddress);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sifre sifirlandi, tum oturumlar kapatildi. KullaniciId: {KullaniciId}", user.Id);

        return Result.Success();
    }
}
