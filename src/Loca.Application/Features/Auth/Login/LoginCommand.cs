using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Giriste sifre karmasikligi DOGRULANMAZ. Kural sonradan sikilastirilirsa
        // eski sifreye sahip kullanicilar giris yapamaz hale gelirdi.
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Sifre zorunludur.");
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    AuthTokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        LoginCommand request, CancellationToken cancellationToken)
    {
        var email = User.Normalize(request.Email);
        var user = await users.GetByEmailAsync(email, cancellationToken);

        // Kullanici yok ile sifre yanlis AYNI cevabi verir. Ayri mesaj
        // verilseydi hangi e-postalarin kayitli oldugu tek tek ogrenilebilirdi.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Basarisiz giris denemesi: {Eposta}", Masking.Email(email));
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Pasif hesapla giris denemesi: {Eposta}", Masking.Email(email));
            return Result.Failure<AuthResponse>(AuthErrors.AccountDisabled);
        }

        user.RecordLogin(dateTimeProvider.UtcNow);

        var roles = user.UserRoles
            .Where(userRole => userRole.Role is not null)
            .Select(userRole => userRole.Role!.Name)
            .ToList();

        var response = tokenIssuer.Issue(user, roles, request.IpAddress);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Giris basarili: {Eposta}", Masking.Email(email));

        return Result.Success(response);
    }
}
