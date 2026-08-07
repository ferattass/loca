using System.Globalization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Admin.Settings;
using Microsoft.Extensions.Configuration;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Odeme ayarlarini veritabani ve yapilandirmayi birlestirerek uretir.
/// </summary>
/// <remarks>
/// <b><c>IOptions</c> KULLANILMIYOR.</b> <c>IOptions</c> degerleri
/// uygulama acilisinda bagliyor ve calisma aninda degismiyor; panelden
/// girilen bir anahtarin etkili olmasi icin uygulamayi yeniden
/// baslatmak gerekirdi. Ayni karar SMTP tarafinda da verilmisti.
///
/// <para>
/// <b>Baslangictaki <c>ValidateOnStart</c> dogrulamasi da kalkti.</b>
/// Anahtarlar artik veritabaninda olabildigi icin acilis aninda
/// "eksik" gorunmeleri normal; uygulamanin bu yuzden hic ayaga
/// kalkmamasi yanlis olurdu. Eksiklik odeme baslatilirken anlasiliyor
/// ve kullaniciya saglayiciya ulasilamadigi soyleniyor.
/// </para>
///
/// <para>
/// Sira: veritabani yapilandirmayi EZER. Panelden bir deger girildiginde
/// onun gecerli olmasi bekleniyor; tersi olsaydi panel calisiyor gorunup
/// hicbir sey degistirmezdi.
/// </para>
/// </remarks>
internal sealed class IyzicoSettingsProvider(
    ISettingsStore settings,
    IConfiguration configuration) : IPaymentSettingsReader
{
    /// <summary>Saglayicinin kullandigi birlesik ayarlar.</summary>
    public async Task<IyzicoOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var (secenekler, _) = await OkuAsync(cancellationToken);

        return secenekler;
    }

    public async Task<OdemeAyarlari> GetAsync(CancellationToken cancellationToken = default)
    {
        var (secenekler, kaynak) = await OkuAsync(cancellationToken);
        var kayitlar = await settings.GetAllAsync(cancellationToken);

        string Ayar(string anahtar, string varsayilan = "")
        {
            if (kayitlar.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger))
                return deger;

            return configuration[anahtar] ?? varsayilan;
        }

        var havale = new HavaleAyarlari(
            Enabled: bool.TryParse(Ayar(PaymentSettingKeys.BankTransferEnabled), out var acik) && acik,
            BankName: Ayar(PaymentSettingKeys.BankTransferBankName),
            AccountName: Ayar(PaymentSettingKeys.BankTransferAccountName),
            Iban: Ayar(PaymentSettingKeys.BankTransferIban),
            DeadlineHours: int.TryParse(
                Ayar(PaymentSettingKeys.BankTransferDeadlineHours),
                CultureInfo.InvariantCulture,
                out var saat)
                ? saat
                : 24);

        // Hizmet bedeli AYNI kaynaktan okunuyor ama hesaplamada
        // ServiceFeeProvider kullaniliyor; burasi yalnizca panelde
        // gostermek icin. Iki yerde okunmasinin sebebi donus tipleri:
        // burada ham sayilar, orada dogrulanmis bir politika nesnesi.
        var yuzde = decimal.TryParse(
            Ayar(PaymentSettingKeys.ServiceFeePercent),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var okunanYuzde)
            ? okunanYuzde
            : 0m;

        var altSinir = decimal.TryParse(
            Ayar(PaymentSettingKeys.ServiceFeeMinPerTicket),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var okunanAltSinir)
            ? okunanAltSinir
            : 0m;

        return new OdemeAyarlari(
            // Calisan saglayici acilista secildi; panelden degistirilemiyor.
            // Panelde anahtar girip "neden calismiyor" diye sormamak icin
            // gorunur olmasi gerekiyor.
            ActiveProvider: configuration["Payment:Provider"] ?? "Mock",
            // Anahtarin KENDISI donmuyor, yalnizca tanimli olup olmadigi.
            HasApiKey: !string.IsNullOrWhiteSpace(secenekler.ApiKey),
            HasSecretKey: !string.IsNullOrWhiteSpace(secenekler.SecretKey),
            UseSandbox: secenekler.UseSandbox,
            CallbackUrl: secenekler.CallbackUrl,
            ReturnUrl: secenekler.ReturnUrl,
            Source: kaynak,
            IyzicoConfigured: secenekler.Yapilandirilmis,
            BankTransfer: havale,
            ServiceFeePercent: yuzde,
            ServiceFeeMinPerTicket: altSinir);
    }

    /// <returns>Birlesik ayarlar ve degerlerin agirlikli olarak nereden geldigi.</returns>
    private async Task<(IyzicoOptions Secenekler, string Kaynak)> OkuAsync(
        CancellationToken cancellationToken)
    {
        var kayitlar = await settings.GetAllAsync(cancellationToken);

        var veritabanindan = false;
        var yapilandirmadan = false;

        string? Oku(string anahtar)
        {
            if (kayitlar.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger))
            {
                veritabanindan = true;
                return deger;
            }

            var yapilandirma = configuration[anahtar];

            if (string.IsNullOrWhiteSpace(yapilandirma))
                return null;

            yapilandirmadan = true;
            return yapilandirma;
        }

        var secenekler = new IyzicoOptions
        {
            ApiKey = Oku(PaymentSettingKeys.IyzicoApiKey) ?? string.Empty,
            SecretKey = Oku(PaymentSettingKeys.IyzicoSecretKey) ?? string.Empty,
            CallbackUrl = Oku(PaymentSettingKeys.IyzicoCallbackUrl) ?? string.Empty,
            ReturnUrl = Oku(PaymentSettingKeys.IyzicoReturnUrl) ?? string.Empty,
            // Varsayilan sandbox: yapilandirma unutulursa yanlislikla
            // canli tahsilat yapmak yerine sandbox'ta kalinir.
            UseSandbox = !bool.TryParse(Oku(PaymentSettingKeys.IyzicoUseSandbox), out var canli) || canli,
        };

        var kaynak = (veritabanindan, yapilandirmadan) switch
        {
            (true, true) => "Mixed",
            (true, false) => "Database",
            (false, true) => "Configuration",
            _ => "None",
        };

        return (secenekler, kaynak);
    }
}
