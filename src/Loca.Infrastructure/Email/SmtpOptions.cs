using System.Globalization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Admin.Settings;

namespace Loca.Infrastructure.Email;

/// <summary>
/// SMTP ayarlari.
/// </summary>
/// <remarks>
/// Degerler once veritabanindan (yonetim paneli), yoksa yapilandirmadan
/// okunuyor. Bu sira onemli: panelden hic dokunulmamis bir kurulum
/// appsettings/user-secrets ile calismaya devam ediyor, panelden bir
/// deger girildigi anda o gecerli oluyor.
/// </remarks>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1025;

    /// <summary>
    /// STARTTLS veya dogrudan SSL kullanilsin mi.
    /// </summary>
    /// <remarks>
    /// Yerel gelistirmede Mailpit sifresiz ve TLS'siz calisiyor; uretimde
    /// bu KAPALI birakilirsa posta sunucusuna giden kullanici adi ve
    /// sifre ag uzerinde duz metin gider.
    /// </remarks>
    public bool UseSsl { get; set; }

    public string? UserName { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Loca";

    public bool YapilandirilmisMi => !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>
/// Ayarlari veritabani ve yapilandirmayi birlestirerek uretir.
/// </summary>
/// <remarks>
/// <c>IOptions</c> kullanilmadi: <c>IOptions</c> degerleri uygulama
/// acilisinda baglar ve calisma aninda degismez. Panelden yapilan bir
/// degisikligin etkili olmasi icin uygulamanin yeniden baslatilmasi
/// gerekirdi.
/// </remarks>
internal sealed class SmtpOptionsProvider(
    ISettingsStore settings,
    Microsoft.Extensions.Configuration.IConfiguration configuration) : ISmtpSettingsReader
{
    public async Task<SmtpOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        var (ayarlar, _) = await OkuAsync(cancellationToken);

        return ayarlar;
    }

    async Task<SmtpAyarlari> ISmtpSettingsReader.GetAsync(CancellationToken cancellationToken)
    {
        var (ayarlar, kaynak) = await OkuAsync(cancellationToken);

        return new SmtpAyarlari(
            ayarlar.Host,
            ayarlar.Port,
            ayarlar.UseSsl,
            ayarlar.UserName,
            // Sifrenin KENDISI degil, tanimli olup olmadigi.
            HasPassword: !string.IsNullOrEmpty(ayarlar.Password),
            ayarlar.FromAddress,
            ayarlar.FromName,
            Source: kaynak,
            IsConfigured: ayarlar.YapilandirilmisMi);
    }

    /// <returns>Birlesik ayarlar ve degerlerin agirlikli olarak nereden geldigi.</returns>
    private async Task<(SmtpOptions Ayarlar, string Kaynak)> OkuAsync(
        CancellationToken cancellationToken)
    {
        var kayitlar = await settings.GetAllAsync(cancellationToken);

        var veritabanindanGelen = false;
        var yapilandirmadanGelen = false;

        string? Oku(string alan)
        {
            var anahtar = $"{SmtpOptions.SectionName}:{alan}";

            // Sira onemli: veritabani yapilandirmayi EZER. Panelden bir
            // deger girildiginde onun gecerli olmasi bekleniyor; tersi
            // olsaydi panel calisiyor gorunup hicbir sey degistirmezdi.
            if (kayitlar.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger))
            {
                veritabanindanGelen = true;
                return deger;
            }

            var yapilandirma = configuration[anahtar];

            if (string.IsNullOrWhiteSpace(yapilandirma))
                return null;

            yapilandirmadanGelen = true;
            return yapilandirma;
        }

        var port = Oku("Port");

        var ayarlar = new SmtpOptions
        {
            Host = Oku("Host") ?? string.Empty,
            Port = int.TryParse(port, CultureInfo.InvariantCulture, out var sayi) ? sayi : 1025,
            UseSsl = bool.TryParse(Oku("UseSsl"), out var ssl) && ssl,
            UserName = Oku("UserName"),
            Password = Oku("Password"),
            FromAddress = Oku("FromAddress") ?? string.Empty,
            FromName = Oku("FromName") ?? "Loca"
        };

        var kaynak = (veritabanindanGelen, yapilandirmadanGelen) switch
        {
            (true, true) => "Mixed",
            (true, false) => "Database",
            (false, true) => "Configuration",
            _ => "None"
        };

        return (ayarlar, kaynak);
    }
}
