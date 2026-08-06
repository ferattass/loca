using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.Settings;

/// <summary>Panelde duzenlenebilen odeme ayarlarinin anahtarlari.</summary>
/// <remarks>
/// <b>Bu anahtarlarin panele acilmasi bilincli bir karar.</b> Veritabani
/// baglanti dizesi ve JWT anahtari HÂLÂ user-secrets'ta: onlar uygulamanin
/// AYAGA KALKMASI icin gerekli ve panelden degistirilebilir olmalari,
/// panele erisen birinin altyapiyi ele gecirmesi demek olurdu.
///
/// <para>
/// Odeme saglayicisi anahtarlari o kumeye girmiyor: uygulama onlarsiz da
/// aciliyor (taklit saglayiciyla) ve bunlar bir isletme karari — saglayici
/// hesabi degistiginde deploy beklemek gerekmemeli. Sirlar sifreli
/// saklaniyor ve okuma uclarindan hicbir zaman donmuyor.
/// </para>
/// </remarks>
public static class PaymentSettingKeys
{
    public const string IyzicoApiKey = "Iyzico:ApiKey";
    public const string IyzicoSecretKey = "Iyzico:SecretKey";
    public const string IyzicoUseSandbox = "Iyzico:UseSandbox";
    public const string IyzicoCallbackUrl = "Iyzico:CallbackUrl";
    public const string IyzicoReturnUrl = "Iyzico:ReturnUrl";

    public const string BankTransferEnabled = "BankTransfer:Enabled";
    public const string BankTransferBankName = "BankTransfer:BankName";
    public const string BankTransferAccountName = "BankTransfer:AccountName";
    public const string BankTransferIban = "BankTransfer:Iban";
    public const string BankTransferDeadlineHours = "BankTransfer:DeadlineHours";

    /// <summary>
    /// Sifreli saklanacak ve okuma uclarindan hic donmeyecek anahtarlar.
    /// </summary>
    /// <remarks>
    /// <c>ApiKey</c> de sir sayiliyor. Teknik olarak istek govdesinde
    /// acikca tasiniyor ve tek basina yetkilendirmeye yetmiyor; ama
    /// SecretKey ile birlikte tek bir hesap kimligi olusturuyor ve
    /// ikisinden birini panelde gostermenin hicbir faydasi yok.
    /// </remarks>
    public static readonly IReadOnlySet<string> Secrets =
        new HashSet<string>(StringComparer.Ordinal) { IyzicoApiKey, IyzicoSecretKey };
}

/// <param name="ActiveProvider">
/// Su an CALISAN saglayici. Bu deger panelden degistirilemiyor ve
/// yalnizca bilgi olarak donuyor: saglayici secimi acilista bir kez
/// yapiliyor, cunku istek basina secilseydi ayni odemenin baslatilmasi
/// ve tamamlanmasi iki farkli saglayiciya dusebilirdi. Panelde anahtar
/// girip "neden calismiyor" diye sormamak icin gorunur olmasi gerekiyor.
/// </param>
/// <param name="HasApiKey">
/// Anahtarin KENDISI hicbir zaman donmez, yalnizca tanimli olup olmadigi.
/// </param>
public sealed record OdemeAyarlari(
    string ActiveProvider,
    bool HasApiKey,
    bool HasSecretKey,
    bool UseSandbox,
    string CallbackUrl,
    string ReturnUrl,
    string Source,
    bool IyzicoConfigured,
    HavaleAyarlari BankTransfer);

/// <param name="DeadlineHours">
/// Havale ile odenen rezervasyonun kac saat ayakta kalacagi.
/// <b>Kart odemesinden ayri olmak zorunda:</b> koltuk kilidi on dakika ve
/// havale banka saatlerine bagli — on dakikalik bir pencerede havale
/// yapilamaz. Bu sure boyunca koltuk odenmemis hâlde tutuluyor; bu bir
/// isletme riski, bu yuzden panelden ayarlanabilir.
/// </param>
public sealed record HavaleAyarlari(
    bool Enabled,
    string BankName,
    string AccountName,
    string Iban,
    int DeadlineHours);

public sealed record GetPaymentSettingsQuery : IRequest<Result<OdemeAyarlari>>;

/// <param name="ApiKey">
/// Bos birakilirsa mevcut anahtar KORUNUR. Panel anahtari hic
/// gostermedigi icin form her acildiginda bu alan bos geliyor.
/// </param>
/// <param name="ClearIyzicoKeys">
/// Kayitli anahtarlari siler. "Bos = koru" kuralinin acik istisnasi;
/// bu olmadan bir kez kaydedilen anahtar hicbir zaman kaldirilamazdi.
/// </param>
public sealed record UpdatePaymentSettingsCommand(
    string? ApiKey,
    string? SecretKey,
    bool UseSandbox,
    string CallbackUrl,
    string ReturnUrl,
    bool BankTransferEnabled,
    string BankName,
    string AccountName,
    string Iban,
    int DeadlineHours,
    bool ClearIyzicoKeys = false) : IRequest<Result>;

internal sealed class UpdatePaymentSettingsCommandValidator
    : AbstractValidator<UpdatePaymentSettingsCommand>
{
    public UpdatePaymentSettingsCommandValidator()
    {
        RuleFor(komut => komut.CallbackUrl)
            .NotEmpty().WithMessage("Geri donus adresi zorunlu.")
            .Must(GecerliAdres).WithMessage("Geri donus adresi gecerli bir URL olmali.")
            .MaximumLength(500);

        RuleFor(komut => komut.ReturnUrl)
            .NotEmpty().WithMessage("Arayuz adresi zorunlu.")
            .Must(GecerliAdres).WithMessage("Arayuz adresi gecerli bir URL olmali.")
            .MaximumLength(500);

        RuleFor(komut => komut.ApiKey).MaximumLength(200);
        RuleFor(komut => komut.SecretKey).MaximumLength(200);

        // Havale kapaliyken banka alanlari bos olabilir; ACIKKEN
        // olamaz. Acik ama IBAN'siz bir yapilandirma, kullaniciya
        // "havale ile ode" deyip nereye odeyecegini soylememek olurdu.
        RuleFor(komut => komut.BankName)
            .NotEmpty().WithMessage("Havale aciksa banka adi zorunlu.")
            .MaximumLength(100)
            .When(komut => komut.BankTransferEnabled);

        RuleFor(komut => komut.AccountName)
            .NotEmpty().WithMessage("Havale aciksa hesap sahibi zorunlu.")
            .MaximumLength(150)
            .When(komut => komut.BankTransferEnabled);

        RuleFor(komut => komut.Iban)
            .NotEmpty().WithMessage("Havale aciksa IBAN zorunlu.")
            .Must(GecerliIban).WithMessage("IBAN TR ile baslamali ve 26 karakter olmali.")
            .When(komut => komut.BankTransferEnabled);

        RuleFor(komut => komut.DeadlineHours)
            .InclusiveBetween(1, 168)
            .WithMessage("Odeme suresi 1 ile 168 saat arasinda olmali.");

        RuleFor(komut => komut.ClearIyzicoKeys)
            .Equal(false)
            .When(komut => !string.IsNullOrEmpty(komut.ApiKey) || !string.IsNullOrEmpty(komut.SecretKey))
            .WithMessage("Anahtar alani doluyken anahtarlari kaldirma secilemez.");
    }

    private static bool GecerliAdres(string? deger) =>
        Uri.TryCreate(deger, UriKind.Absolute, out var adres)
        && (adres.Scheme == Uri.UriSchemeHttp || adres.Scheme == Uri.UriSchemeHttps);

    /// <remarks>
    /// Bicim kontrolu, gecerlilik kontrolu DEGIL: banka hesabinin
    /// gercekten var oldugunu ancak banka soyleyebilir. Buradaki kontrol
    /// yalnizca yazim hatasini yakaliyor — bosluklar temizlendikten
    /// sonra TR + 24 hane.
    /// </remarks>
    private static bool GecerliIban(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
            return false;

        var temiz = deger.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        return temiz.Length == 26
            && temiz.StartsWith("TR", StringComparison.Ordinal)
            && temiz[2..].All(char.IsAsciiDigit);
    }
}

/// <summary>
/// Odeme ayarlarinin panelde gosterilecek hâlini uretir.
/// </summary>
/// <remarks>
/// Uygulamasi altyapida cunku degerin nereden geldigini (veritabani mi
/// yapilandirma mi) ve hangi saglayicinin calistigini yalnizca ikisini de
/// goren taraf bilebiliyor.
/// </remarks>
public interface IPaymentSettingsReader
{
    Task<OdemeAyarlari> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class GetPaymentSettingsQueryHandler(IPaymentSettingsReader reader)
    : IRequestHandler<GetPaymentSettingsQuery, Result<OdemeAyarlari>>
{
    public async Task<Result<OdemeAyarlari>> Handle(
        GetPaymentSettingsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await reader.GetAsync(cancellationToken));
}

internal sealed class UpdatePaymentSettingsCommandHandler(
    ISettingsStore settings,
    ICurrentUserService currentUser,
    ILogger<UpdatePaymentSettingsCommandHandler> logger)
    : IRequestHandler<UpdatePaymentSettingsCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePaymentSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var degerler = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PaymentSettingKeys.IyzicoApiKey] = request.ApiKey,
            [PaymentSettingKeys.IyzicoSecretKey] = request.SecretKey,
            [PaymentSettingKeys.IyzicoUseSandbox] = request.UseSandbox ? "true" : "false",
            [PaymentSettingKeys.IyzicoCallbackUrl] = request.CallbackUrl.Trim(),
            [PaymentSettingKeys.IyzicoReturnUrl] = request.ReturnUrl.Trim(),

            [PaymentSettingKeys.BankTransferEnabled] = request.BankTransferEnabled ? "true" : "false",
            [PaymentSettingKeys.BankTransferBankName] = request.BankName.Trim(),
            [PaymentSettingKeys.BankTransferAccountName] = request.AccountName.Trim(),
            // Bosluklar temizleniyor: IBAN kopyalanirken gruplu geliyor
            // ve kullaniciya gosterilen bicimi biz kuruyoruz.
            [PaymentSettingKeys.BankTransferIban] =
                request.Iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
            [PaymentSettingKeys.BankTransferDeadlineHours] =
                request.DeadlineHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var silinecekler = request.ClearIyzicoKeys
            ? PaymentSettingKeys.Secrets
            : (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);

        await settings.SetAsync(
            degerler, PaymentSettingKeys.Secrets, silinecekler, cancellationToken);

        // Anahtarlar loglanmiyor; degisiklik yapildigi ve kimin yaptigi
        // bilgisi denetim icin yeterli.
        logger.LogInformation(
            "Odeme ayarlari guncellendi. Sandbox: {Sandbox}, Havale: {Havale}, "
                + "AnahtarKaldirildi: {Kaldirildi}, YapanId: {YapanId}",
            request.UseSandbox,
            request.BankTransferEnabled,
            request.ClearIyzicoKeys,
            currentUser.UserId);

        return Result.Success();
    }
}
