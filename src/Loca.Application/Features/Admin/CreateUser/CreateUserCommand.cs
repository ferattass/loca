using System.Security.Cryptography;
using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Common.Logging;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.CreateUser;

/// <param name="ResetLinkSent">
/// Sifirlama postasinin gonderilip gonderilmedigi. <b>Hesap yine de
/// acilir:</b> posta sunucusu tanimli degilse (yerelde sik) hesabin hic
/// olusmamasi, yoneticiyi once SMTP kurmaya zorlardi. Ekran bu bayraga
/// bakip "posta gitmedi, sifirlama baglantisini elle gonder" diyor.
/// </param>
public sealed record OlusturulanHesap(Guid UserId, string Email, bool ResetLinkSent);

/// <summary>
/// Yonetici, organizator/sanatci icin hesap acar.
/// </summary>
/// <remarks>
/// <b>Sifre panelde belirlenmiyor ve gosterilmiyor.</b> Yonetici bir sifre
/// yazsaydi o sifreyi kullaniciya bir kanaldan iletmesi gerekirdi (WhatsApp,
/// telefon) ve o kanalda kalici olarak dururdu; ustelik yonetici hesabin
/// sifresini bilmeye devam ederdi. Bunun yerine rastgele ve kimsenin
/// gormedigi bir sifre uretiliyor, kullaniciya sifirlama baglantisi
/// gonderiliyor — ilk sifreyi hesabin sahibi koyuyor.
///
/// <para>
/// Kayit ucundan (<c>POST /auth/register</c>) farki: orada kullanici kendi
/// hesabini aciyor ve her zaman <c>Customer</c> oluyor. Burada yonetici
/// baskasi adina aciyor ve rolu de belirliyor — bu yuzden ayri bir uc ve
/// <c>AdminOnly</c>.
/// </para>
/// </remarks>
public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles) : IRequest<Result<OlusturulanHesap>>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(komut => komut.Email)
            .NotEmpty().WithMessage("E-posta zorunlu.")
            .EmailAddress().WithMessage("Gecerli bir e-posta adresi girin.")
            .MaximumLength(256);

        RuleFor(komut => komut.FullName)
            .NotEmpty().WithMessage("Ad soyad zorunlu.")
            .MaximumLength(150);

        RuleFor(komut => komut.PhoneNumber).MaximumLength(30);

        RuleFor(komut => komut.Roles)
            .NotEmpty().WithMessage("En az bir rol secilmeli.");

        RuleForEach(komut => komut.Roles)
            .Must(ad => RoleNames.All.Contains(ad, StringComparer.Ordinal))
            .WithMessage($"Rol su degerlerden biri olmali: {string.Join(", ", RoleNames.All)}");

        // ADMIN ROLU BU UCTAN VERILEMEZ. Hesap acma ve yetki yukseltme ayri
        // isler: buradan verilebilseydi panele erisen biri kendi kontrol
        // ettigi bir adrese admin hesabi acip kalici erisim birakabilirdi.
        // Var olan bir kullaniciya admin vermek yine mumkun ama o ayri bir
        // uctan (rol degistirme) ve ayri bir kayitla oluyor.
        RuleFor(komut => komut.Roles)
            .Must(roller => !roller.Contains(RoleNames.Admin, StringComparer.Ordinal))
            .WithMessage("Admin rolu hesap acarken verilemez; once hesap acilip sonra atanir.");
    }
}

internal static class CreateUserErrors
{
    internal static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Admin.EmailAlreadyRegistered", "Bu e-posta adresi zaten kayitli.");

    /// <remarks>
    /// Dogrulama rol adini zaten listeyle karsilastiriyor; buraya dusmek
    /// rolun veritabaninda olmadigi anlamina geliyor, yani migration
    /// tohumlamasi eksik. Sessizce rolsuz hesap acmak yerine acikca hata.
    /// </remarks>
    internal static readonly Error RoleNotFound =
        Error.Conflict("Admin.RoleNotFound", "Rol veritabaninda bulunamadi.");
}

internal sealed class CreateUserCommandHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IPasswordResetTokenGenerator tokenGenerator,
    IPasswordResetNotifier notifier,
    ICurrentUserService currentUser,
    ILogger<CreateUserCommandHandler> logger)
    : IRequestHandler<CreateUserCommand, Result<OlusturulanHesap>>
{
    public async Task<Result<OlusturulanHesap>> Handle(
        CreateUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = User.Normalize(request.Email);

        if (await users.EmailExistsAsync(email, cancellationToken))
            return Result.Failure<OlusturulanHesap>(CreateUserErrors.EmailAlreadyRegistered);

        // Roller once cozuluyor: biri bulunamazsa kullanici hic
        // olusturulmasin. Once kullaniciyi acip sonra rol atamak,
        // yazim hatasi olan bir rolde rolsuz bir hesap birakirdi.
        var roller = new List<Role>(request.Roles.Count);

        foreach (var rolAdi in request.Roles.Distinct(StringComparer.Ordinal))
        {
            var rol = await users.GetRoleByNameAsync(rolAdi, cancellationToken);

            if (rol is null)
                return Result.Failure<OlusturulanHesap>(CreateUserErrors.RoleNotFound);

            roller.Add(rol);
        }

        var kullanici = new User(
            email,
            // Rastgele ve HICBIR YERE yazilmayan sifre. Hesap bu sifreyle
            // acilamaz; girisin tek yolu sifirlama baglantisi. Bos birakmak
            // yerine rastgele deger konuyor cunku bos ozet, dogrulama
            // mantiginda bir gun "sifre kontrolu atlansin" hâline
            // donusebilirdi.
            passwordHasher.Hash(GeciciSifre()),
            request.FullName,
            request.PhoneNumber);

        foreach (var rol in roller)
            kullanici.AssignRole(rol);

        users.Add(kullanici);

        var uretilen = tokenGenerator.Create();

        resetTokens.Add(new PasswordResetToken(
            kullanici.Id, uretilen.Hash, uretilen.ExpiresAt, currentUser.IpAddress));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Postanin gitmemesi hesabi geri almaz: yonetici baglantiyi baska
        // bir yoldan iletebilir ve kullanici "sifremi unuttum" ile de yeni
        // baglanti isteyebilir.
        var postaGitti = true;

        try
        {
            await notifier.SendAsync(
                kullanici.Email, uretilen.Value, uretilen.ExpiresAt, cancellationToken);
        }
        catch (InvalidOperationException hata)
        {
            postaGitti = false;
            logger.LogWarning(hata, "Hesap acildi ama sifirlama postasi gonderilemedi.");
        }

        logger.LogInformation(
            "Yonetici hesap acti. Eposta: {Eposta}, Roller: {Roller}, AcanId: {AcanId}",
            Masking.Email(kullanici.Email),
            string.Join(",", request.Roles),
            currentUser.UserId);

        return Result.Success(new OlusturulanHesap(kullanici.Id, kullanici.Email, postaGitti));
    }

    /// <summary>Kimsenin gormedigi, tek kullanimlik olmayan bir yer tutucu.</summary>
    private static string GeciciSifre() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
