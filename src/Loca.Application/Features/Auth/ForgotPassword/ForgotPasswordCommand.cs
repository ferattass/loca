using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Loca.Application.Common.Models;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email,
    string? IpAddress) : IRequest<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() =>
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Gecerli bir e-posta adresi girin.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");
}

internal sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IUnitOfWork unitOfWork,
    IPasswordResetTokenGenerator tokenGenerator,
    IPasswordResetNotifier notifier,
    IDateTimeProvider dateTimeProvider,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = User.Normalize(request.Email);
        var user = await users.GetByEmailAsync(email, cancellationToken);

        // Kayitli olmayan adres icin de basarili donulur. "Boyle bir kullanici
        // yok" cevabi, hangi e-postalarin sistemde oldugunu tek tek denemeye
        // acik hale getirirdi — giris ucundaki ayni gerekce.
        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Kayitsiz veya pasif adres icin sifirlama istendi: {Eposta}", Masking.Email(email));

            return Result.Success();
        }

        var now = dateTimeProvider.UtcNow;

        // Onceki baglantilar gecersiz kilinir: kullanici pes pese uc istek
        // atmissa e-posta kutusunda ayni anda calisan uc baglanti kalmasin,
        // yalnizca sonuncusu gecerli olsun.
        var previous = await resetTokens.GetUsableByUserIdAsync(user.Id, now, cancellationToken);
        foreach (var token in previous)
            token.MarkUsed(now);

        var generated = tokenGenerator.Create();

        resetTokens.Add(new PasswordResetToken(
            user.Id, generated.Hash, generated.ExpiresAt, request.IpAddress));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Duz token yalnizca burada, bildirim kanalina verilir; veritabaninda
        // ve log'da ozeti bile degil, hicbir izi yok.
        await notifier.SendAsync(user.Email, generated.Value, generated.ExpiresAt, cancellationToken);

        logger.LogInformation(
            "Sifre sifirlama token'i uretildi. KullaniciId: {KullaniciId}", user.Id);

        return Result.Success();
    }
}
