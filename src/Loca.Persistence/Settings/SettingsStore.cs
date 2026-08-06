using Loca.Application.Common.Interfaces;
using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loca.Persistence.Settings;

/// <summary>
/// Ayarlari veritabanindan okur, onbellekte tutar.
/// </summary>
/// <remarks>
/// Onbellek suresi kisa (30 sn) ve yazmada dusuruluyor. Sonsuz onbellek
/// tek sunucuda dogru calisirdi ama birden fazla ornek calistiginda
/// diger orneklerin degisiklikten haberi olmazdi; kisa sure, panelden
/// yapilan bir degisikligin en gec yarim dakikada her yerde gecerli
/// olmasini sagliyor.
/// </remarks>
internal sealed class SettingsStore(
    LocaDbContext context,
    IMemoryCache cache,
    ISecretProtector protector,
    ILogger<SettingsStore> logger) : ISettingsStore
{
    private const string OnbellekAnahtari = "app-settings";
    private static readonly TimeSpan OnbellekSuresi = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(OnbellekAnahtari, out IReadOnlyDictionary<string, string?>? onbellekli) &&
            onbellekli is not null)
        {
            return onbellekli;
        }

        var satirlar = await context.Set<AppSetting>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sozluk = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var satir in satirlar)
        {
            sozluk[satir.Key] = satir.IsSecret && satir.Value is { Length: > 0 } sifreli
                ? Coz(satir.Key, sifreli)
                : satir.Value;
        }

        cache.Set(OnbellekAnahtari, (IReadOnlyDictionary<string, string?>)sozluk, OnbellekSuresi);

        return sozluk;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var tumu = await GetAllAsync(cancellationToken);

        return tumu.TryGetValue(key, out var deger) ? deger : null;
    }

    public async Task SetAsync(
        IReadOnlyDictionary<string, string?> values,
        IReadOnlySet<string> secrets,
        IReadOnlySet<string>? clearedSecrets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(secrets);

        var silinecekler = clearedSecrets ?? new HashSet<string>(StringComparer.Ordinal);

        var anahtarlar = values.Keys.ToList();

        var mevcutlar = await context.Set<AppSetting>()
            .Where(setting => anahtarlar.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, cancellationToken);

        foreach (var (anahtar, deger) in values)
        {
            var sir = secrets.Contains(anahtar);
            var siliniyor = silinecekler.Contains(anahtar);

            var saklanacak = sir && !siliniyor && deger is { Length: > 0 }
                ? protector.Protect(deger)
                : siliniyor ? null : deger;

            // Sir alanda BOS deger "degistirme" demek, "sil" demek degil.
            // Panel sirri hic gostermedigi icin form her acildiginda o
            // alan bos geliyor; bos gelen degeri yazsaydik kullanici baska
            // bir alani duzelttiginde sifreyi farkinda olmadan silerdi.
            // Silme AYRI VE ACIK bir istek olarak geliyor.
            var atlansin = sir && !siliniyor && string.IsNullOrEmpty(deger);

            if (mevcutlar.TryGetValue(anahtar, out var mevcut))
            {
                if (atlansin)
                    continue;

                mevcut.SetValue(saklanacak);
            }
            else
            {
                // Hic yazilmamis bir sirri silmek istemek hata degil,
                // sonucu zaten istenen durum: satir acilmiyor.
                if (atlansin || siliniyor)
                    continue;

                context.Set<AppSetting>().Add(new AppSetting(anahtar, saklanacak, sir));
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        cache.Remove(OnbellekAnahtari);
    }

    /// <remarks>
    /// Cozulemeyen bir sir sessizce yok sayiliyor ve <c>null</c> donuyor:
    /// Data Protection anahtari degistiginde (yeniden kurulum, baska
    /// makine) eski deger cozulemez hâle geliyor. Istisna firlatilsaydi
    /// uygulama acilista cokerdi; oysa dogru davranis "bu ayar tanimli
    /// degil" deyip yoneticinin yeniden girmesini beklemek.
    /// </remarks>
    private string? Coz(string anahtar, string sifreli)
    {
        var cozulen = protector.Unprotect(sifreli);

        if (cozulen is null)
        {
            logger.LogWarning(
                "Sir ayar cozulemedi, tanimsiz sayiliyor. Anahtar: {Anahtar}", anahtar);
        }

        return cozulen;
    }
}
