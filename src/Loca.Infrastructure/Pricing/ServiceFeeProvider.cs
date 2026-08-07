using System.Globalization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Admin.Settings;
using Loca.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loca.Infrastructure.Pricing;

/// <summary>
/// Hizmet bedelini panel ayarindan, yoksa yapilandirmadan okur.
/// </summary>
/// <remarks>
/// Ayni katmanli okuma <c>IyzicoSettingsProvider</c>'da da var: panel
/// degeri varsa o, yoksa <c>appsettings</c>. Boylece panele hic
/// dokunulmamis bir kurulum calismaya devam ediyor.
///
/// <para>
/// <b>Okunamayan deger sessizce sifira dusuyor, hataya degil.</b> Bozuk
/// bir ayar yuzunden odeme akisinin tamamen durmasi, birkac kurus hizmet
/// bedeli kaybetmekten cok daha pahali. Durum log'a yaziliyor.
/// </para>
/// </remarks>
internal sealed class ServiceFeeProvider(
    ISettingsStore settings,
    IConfiguration configuration,
    ILogger<ServiceFeeProvider> logger) : IServiceFeeProvider
{
    public async Task<ServiceFeePolicy> GetAsync(CancellationToken cancellationToken = default)
    {
        var kayitlar = await settings.GetAllAsync(cancellationToken);

        var yuzde = Oku(kayitlar, PaymentSettingKeys.ServiceFeePercent);
        var altSinir = Oku(kayitlar, PaymentSettingKeys.ServiceFeeMinPerTicket);

        try
        {
            return new ServiceFeePolicy(yuzde, altSinir);
        }
        catch (DomainException hata)
        {
            logger.LogError(
                hata,
                "Hizmet bedeli ayari gecersiz (yuzde: {Yuzde}, alt sinir: {AltSinir}). "
                    + "Bedel alinmayacak.",
                yuzde,
                altSinir);

            return ServiceFeePolicy.None;
        }
    }

    private decimal Oku(IReadOnlyDictionary<string, string?> kayitlar, string anahtar)
    {
        var ham = kayitlar.TryGetValue(anahtar, out var panelden)
            && !string.IsNullOrWhiteSpace(panelden)
                ? panelden
                : configuration[anahtar];

        if (string.IsNullOrWhiteSpace(ham))
            return 0m;

        // InvariantCulture ZORUNLU: makinenin dili Turkce oldugunda "8.5"
        // ondalik ayraci nokta olmadigi icin 85 olarak okunurdu ve hizmet
        // bedeli on kat cikardi.
        if (decimal.TryParse(ham, NumberStyles.Number, CultureInfo.InvariantCulture, out var deger))
            return deger;

        logger.LogWarning("Hizmet bedeli ayari sayiya cevrilemedi: {Anahtar} = {Deger}", anahtar, ham);

        return 0m;
    }
}
