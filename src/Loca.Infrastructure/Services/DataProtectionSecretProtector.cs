using Loca.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Loca.Infrastructure.Services;

/// <summary>
/// Sir ayarlari ASP.NET Core Data Protection ile sifreler.
/// </summary>
/// <remarks>
/// Amac tam olarak su: veritabani yedegini eline geciren biri SMTP
/// sifresini okuyamasin. Anahtar malzemesi veritabaninda degil
/// uygulamanin anahtar deposunda duruyor, yani ikisini birden ele
/// gecirmek gerekiyor.
///
/// <para>
/// <b>Amac kullaniciya karsi gizlilik degil.</b> Uygulamanin kendisi bu
/// degeri cozebiliyor; sunucuya erisen biri de cozebilir. Bu yuzden sir
/// ayarlar okuma uclarindan hicbir zaman donmuyor — sifreleme tek
/// basina yeterli bir savunma degil.
/// </para>
///
/// <para>
/// Amac ayrimi ("Loca.Settings"): ayni anahtarla sifrelenmis baska bir
/// veri (orn. bir cerez) buraya kopyalanip cozdurulemesin.
/// </para>
/// </remarks>
internal sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Amac = "Loca.Settings";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _protector = provider.CreateProtector(Amac);
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        return _protector.Protect(plaintext);
    }

    public string? Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Anahtar degismis veya deger bozulmus. Cagiran taraf bunu
            // "ayar tanimli degil" olarak ele aliyor; istisna yukari
            // birakilsaydi ayarlari okuyan her istek cokerdi.
            return null;
        }
    }
}
