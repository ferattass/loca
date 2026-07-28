using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Auth.Register;

/// <param name="IpAddress">
/// Istek govdesinden degil, controller tarafindan baglantidan okunur.
/// Istemcinin bildirdigi bir IP'ye guvenilmez.
/// </param>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber,
    string? IpAddress) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Gecerli bir e-posta adresi girin.")
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Sifre zorunludur.")
            .MinimumLength(8).WithMessage("Sifre en az 8 karakter olmali.")
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("Sifre en az bir harf icermeli.")
            .Matches("[0-9]").WithMessage("Sifre en az bir rakam icermeli.");

        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MinimumLength(3)
            .MaximumLength(150);

        RuleFor(command => command.PhoneNumber)
            .MaximumLength(20)
            .Matches(@"^[0-9+()\s-]+$").WithMessage("Telefon numarasi gecersiz.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
    }
}

internal sealed class RegisterCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    AuthTokenIssuer tokenIssuer,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = User.Normalize(request.Email);

        // Bu kontrol kullaniciya anlamli mesaj vermek icin. Gercek koruma
        // Users.Email uzerindeki UNIQUE index; iki eszamanli kayit denemesinde
        // ikisi de bu kontrolu gecebilir, index gecemez.
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            logger.LogInformation(
                "Kayitli e-posta ile yeni kayit denendi: {Eposta}", Masking.Email(email));

            return Result.Failure<AuthResponse>(AuthErrors.EmailAlreadyRegistered);
        }

        var customerRole = await users.GetRoleByNameAsync(RoleNames.Customer, cancellationToken)
            ?? throw new InvalidOperationException(
                $"'{RoleNames.Customer}' rolu veritabaninda yok. Migration tohumlamasi calismamis olabilir.");

        var user = new User(
            email,
            passwordHasher.Hash(request.Password),
            request.FullName,
            request.PhoneNumber);

        // Herkes musteri olarak baslar. Organizator rolu, admin onayindan
        // sonra ayrica atanir (organizator basvurusu — Gun 4).
        user.AssignRole(customerRole);
        users.Add(user);

        var response = tokenIssuer.Issue(user, [RoleNames.Customer], request.IpAddress);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Yeni kullanici kaydi: {Eposta}", Masking.Email(email));

        return Result.Success(response);
    }
}
