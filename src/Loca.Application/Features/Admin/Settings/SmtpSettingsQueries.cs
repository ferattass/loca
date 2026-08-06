using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.Settings;

/// <summary>
/// SMTP ayarlarinin panelde gosterilen hâli.
/// </summary>
/// <param name="HasPassword">
/// Sifre tanimli mi. <b>Sifrenin kendisi HICBIR ZAMAN donmez.</b> Donseydi
/// panele erisen herkes posta hesabinin sifresini okuyabilirdi ve o sifre
/// baska yerlerde de kullaniliyor olabilir.
/// </param>
/// <param name="Source">
/// Degerin nereden geldigi: <c>Database</c> panelden girilmis,
/// <c>Configuration</c> appsettings/user-secrets'tan geliyor,
/// <c>None</c> hic tanimli degil. Yonetici "ben girmedim ama calisiyor"
/// durumunu anlayabilmeli.
/// </param>
public sealed record SmtpAyarlari(
    string Host,
    int Port,
    bool UseSsl,
    string? UserName,
    bool HasPassword,
    string FromAddress,
    string FromName,
    string Source,
    bool IsConfigured);

public sealed record GetSmtpSettingsQuery : IRequest<Result<SmtpAyarlari>>;

/// <param name="Password">
/// Bos birakilirsa mevcut sifre <b>korunur</b>, silinmez. Panel sifreyi
/// hic gostermedigi icin form her acildiginda bu alan bos geliyor; bos
/// degeri yazsaydik yonetici baska bir alani duzelttiginde sifreyi
/// farkinda olmadan silerdi.
/// </param>
/// <param name="ClearPassword">
/// Kayitli sifreyi siler. "Bos = koru" kuralinin tek istisnasi ve
/// <b>acikca istenmesi gerekiyor</b>: bu olmadan bir kez kaydedilmis
/// sifre hicbir zaman kaldirilamazdi, kimlik dogrulamali bir sunucudan
/// dogrulamasiz birine gecen yonetici eski sifreyi veritabaninda birakmak
/// zorunda kalirdi.
/// </param>
public sealed record UpdateSmtpSettingsCommand(
    string Host,
    int Port,
    bool UseSsl,
    string? UserName,
    string? Password,
    string FromAddress,
    string FromName,
    bool ClearPassword = false) : IRequest<Result>;

internal sealed class UpdateSmtpSettingsCommandValidator
    : AbstractValidator<UpdateSmtpSettingsCommand>
{
    public UpdateSmtpSettingsCommandValidator()
    {
        RuleFor(komut => komut.Host)
            .NotEmpty().WithMessage("Sunucu adresi zorunlu.")
            .MaximumLength(200);

        RuleFor(komut => komut.Port)
            .InclusiveBetween(1, 65535).WithMessage("Port 1 ile 65535 arasinda olmali.");

        RuleFor(komut => komut.FromAddress)
            .NotEmpty().WithMessage("Gonderen adresi zorunlu.")
            .EmailAddress().WithMessage("Gonderen adresi gecerli bir e-posta olmali.")
            .MaximumLength(200);

        RuleFor(komut => komut.FromName)
            .NotEmpty().WithMessage("Gonderen adi zorunlu.")
            .MaximumLength(100);

        RuleFor(komut => komut.UserName).MaximumLength(200);
        RuleFor(komut => komut.Password).MaximumLength(500);

        // Hem yeni sifre yazip hem "sifreyi kaldir" demek celiskili bir
        // istek. Biri sessizce kazansaydi yonetici hangisinin gecerli
        // oldugunu ancak deneyerek anlardi; istek reddediliyor.
        RuleFor(komut => komut.ClearPassword)
            .Equal(false)
            .When(komut => !string.IsNullOrEmpty(komut.Password))
            .WithMessage("Sifre alani doluyken sifreyi kaldirma secilemez.");
    }
}

/// <summary>Baglantiyi dener, posta gondermez.</summary>
public sealed record TestSmtpConnectionCommand : IRequest<Result<SmtpTestSonucu>>;

/// <param name="Error">Basarisizsa sunucunun verdigi mesaj; basariliysa <c>null</c>.</param>
public sealed record SmtpTestSonucu(bool Succeeded, string? Error);
