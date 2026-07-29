using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string? IpAddress) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut sifre zorunludur.");

        // Kayitla ayni kurallar. Sifre degistirme, politikanin delindigi
        // arka kapi olmamali.
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("Yeni sifre zorunludur.")
            .MinimumLength(8).WithMessage("Sifre en az 8 karakter olmali.")
            .MaximumLength(128).WithMessage("Sifre en fazla 128 karakter olabilir.")
            .Matches("[A-Za-z]").WithMessage("Sifre en az bir harf icermeli.")
            .Matches("[0-9]").WithMessage("Sifre en az bir rakam icermeli.");

        RuleFor(command => command.NewPassword)
            .NotEqual(command => command.CurrentPassword)
            .WithMessage("Yeni sifre eskisiyle ayni olamaz.");
    }
}

internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser,
    ILogger<ChangePasswordCommandHandler> logger)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(
        ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure(AuthErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
            return Result.Failure(AuthErrors.InvalidCredentials);

        // Oturum acik olsa bile mevcut sifre soruluyor: acik birakilmis bir
        // ekran basinda oturan biri sifreyi degistirip hesabi ele geciremesin.
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            logger.LogWarning(
                "Hatali mevcut sifre ile degistirme denemesi. KullaniciId: {KullaniciId}", userId);

            return Result.Failure(AuthErrors.CurrentPasswordIncorrect);
        }

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));

        await RevokeAllSessionsAsync(userId, request.IpAddress, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sifre degistirildi, tum oturumlar kapatildi. KullaniciId: {KullaniciId}", userId);

        return Result.Success();
    }

    /// <remarks>
    /// Sifre degistirmenin amaci, sifreyi bilen birinin erisimini kesmek.
    /// Eski sifreyle acilmis refresh token'lar ayakta kalsaydi saldirgan
    /// yedi gun boyunca oturumunu yenilemeye devam ederdi.
    /// </remarks>
    private async Task RevokeAllSessionsAsync(
        Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var active = await refreshTokens.GetActiveByUserIdAsync(userId, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        foreach (var token in active)
            token.Revoke(now, RefreshTokenRevokeReason.PasswordChanged, revokedByIp: ipAddress);
    }
}
