using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.Settings;

/// <summary>
/// SMTP ayarlarinin panelde gosterilecek hâlini uretir.
/// </summary>
/// <remarks>
/// Uygulamasi altyapida cunku degerin nereden geldigini (veritabani mi
/// yapilandirma mi) yalnizca ikisini de goren taraf bilebiliyor.
/// </remarks>
public interface ISmtpSettingsReader
{
    Task<SmtpAyarlari> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Panelde duzenlenebilen SMTP anahtarlari.</summary>
/// <remarks>
/// Anahtar adlari tek yerde: hem yazan komut hem okuyan saglayici bu
/// listeye bakiyor. Iki yerde yazilsaydi birinde yapilan bir yazim hatasi
/// "kaydediliyor ama okunmuyor" gibi tarif edilmesi zor bir arizaya
/// donusurdu.
/// </remarks>
public static class SmtpSettingKeys
{
    public const string Host = "Smtp:Host";
    public const string Port = "Smtp:Port";
    public const string UseSsl = "Smtp:UseSsl";
    public const string UserName = "Smtp:UserName";
    public const string Password = "Smtp:Password";
    public const string FromAddress = "Smtp:FromAddress";
    public const string FromName = "Smtp:FromName";

    /// <summary>Sifreli saklanacak ve okuma uclarindan hic donmeyecek anahtarlar.</summary>
    public static readonly IReadOnlySet<string> Secrets =
        new HashSet<string>(StringComparer.Ordinal) { Password };
}

internal sealed class GetSmtpSettingsQueryHandler(ISmtpSettingsReader reader)
    : IRequestHandler<GetSmtpSettingsQuery, Result<SmtpAyarlari>>
{
    public async Task<Result<SmtpAyarlari>> Handle(
        GetSmtpSettingsQuery request, CancellationToken cancellationToken)
    {
        var ayarlar = await reader.GetAsync(cancellationToken);

        return Result.Success(ayarlar);
    }
}

internal sealed class UpdateSmtpSettingsCommandHandler(
    ISettingsStore settings,
    ICurrentUserService currentUser,
    ILogger<UpdateSmtpSettingsCommandHandler> logger)
    : IRequestHandler<UpdateSmtpSettingsCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSmtpSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var degerler = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SmtpSettingKeys.Host] = request.Host.Trim(),
            [SmtpSettingKeys.Port] = request.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [SmtpSettingKeys.UseSsl] = request.UseSsl ? "true" : "false",
            [SmtpSettingKeys.UserName] = request.UserName?.Trim(),
            [SmtpSettingKeys.Password] = request.Password,
            [SmtpSettingKeys.FromAddress] = request.FromAddress.Trim(),
            [SmtpSettingKeys.FromName] = request.FromName.Trim()
        };

        var silinecekler = request.ClearPassword
            ? SmtpSettingKeys.Secrets
            : (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);

        await settings.SetAsync(
            degerler, SmtpSettingKeys.Secrets, silinecekler, cancellationToken);

        // Sifre ve kullanici adi loglanmiyor; degisiklik yapildigi bilgisi
        // yeterli. Kim yaptigi ayrica denetim icin gerekiyor.
        logger.LogInformation(
            "SMTP ayarlari guncellendi. Sunucu: {Sunucu}:{Port}, SifreKaldirildi: {Kaldirildi}, YapanId: {YapanId}",
            request.Host,
            request.Port,
            request.ClearPassword,
            currentUser.UserId);

        return Result.Success();
    }
}

internal sealed class TestSmtpConnectionCommandHandler(IEmailSender sender)
    : IRequestHandler<TestSmtpConnectionCommand, Result<SmtpTestSonucu>>
{
    public async Task<Result<SmtpTestSonucu>> Handle(
        TestSmtpConnectionCommand request, CancellationToken cancellationToken)
    {
        var hata = await sender.TestConnectionAsync(cancellationToken);

        // Basarisiz baglanti HATA DEGIL veri olarak donuyor: yonetici
        // ekranda sebebi gormek istiyor ve bu bir sunucu arizasi degil,
        // denemenin sonucu.
        return Result.Success(new SmtpTestSonucu(hata is null, hata));
    }
}
