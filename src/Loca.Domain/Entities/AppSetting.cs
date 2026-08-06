using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Calisma aninda degistirilebilen ayar.
/// </summary>
/// <remarks>
/// <b>Her ayar buraya girmez.</b> Veritabani baglanti dizesi, JWT anahtari
/// ve odeme saglayicisi API anahtarlari user-secrets'ta kalir: bunlar
/// uygulamanin AYAGA KALKMASI icin gerekli ve panelden degistirilebilir
/// olmalari, panele erisen birinin altyapiyi ele gecirmesi demek olurdu.
///
/// <para>
/// Buraya giren ayarlar isletme kararlaridir: SMTP sunucusu, kilit
/// suresi, koltuk limiti. Bunlarin degismesi icin deploy beklemek,
/// "bugun bir saatligine limiti artiralim" gibi normal bir istegi
/// imkansiz kilardi.
/// </para>
/// </remarks>
public sealed class AppSetting : BaseEntity
{
    private AppSetting()
    {
        Key = string.Empty;
    }

    public AppSetting(string key, string? value, bool isSecret)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Ayar anahtari bos olamaz.");

        Key = key.Trim();
        Value = value;
        IsSecret = isSecret;
    }

    /// <summary>Ayarin adi, <c>Smtp:Host</c> gibi.</summary>
    public string Key { get; private set; }

    /// <summary>
    /// Ayarin degeri. Sir olan ayarlarda <b>sifrelenmis</b> hâlde durur.
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>
    /// Bu ayar bir sir mi.
    /// </summary>
    /// <remarks>
    /// Sir olan ayarin degeri hicbir okuma ucundan DONMEZ; panel yalnizca
    /// "tanimli mi" bilgisini gorur. Donseydi panele erisen herkes posta
    /// hesabinin sifresini okuyabilirdi ve o sifre baska yerlerde de
    /// kullaniliyor olabilir.
    /// </remarks>
    public bool IsSecret { get; private set; }

    public void SetValue(string? value) => Value = value;
}
