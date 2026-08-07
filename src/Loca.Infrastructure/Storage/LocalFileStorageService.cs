using System.Globalization;
using Loca.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loca.Infrastructure.Storage;

/// <summary>
/// Dosyalari yerel diske yazar.
/// </summary>
/// <remarks>
/// Dosyalar tarihe gore klasorlenir (<c>2026/07</c>). Hepsi tek klasore
/// yazilsaydi zamanla on binlerce dosya iceren bir dizin olusur ve
/// listeleme islemleri yavaslardi.
/// </remarks>
internal sealed class LocalFileStorageService(
    IOptions<StorageOptions> options,
    IDateTimeProvider dateTimeProvider,
    ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private readonly StorageOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<StoredFile> SaveAsync(
        Stream icerik, string uzanti, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(icerik);

        var now = dateTimeProvider.UtcNow;

        // Klasor adi kulturden bagimsiz uretiliyor: rakamlarin yazimi
        // calisan makinenin diline gore degisirse ayni ay iki farkli
        // klasore yazilir ve eski dosyalar bulunamaz hale gelir.
        var altKlasor = Path.Combine(
            now.Year.ToString(CultureInfo.InvariantCulture),
            now.Month.ToString("00", CultureInfo.InvariantCulture));

        // Ad burada uretiliyor; kullanicinin verdigi ad hicbir sekilde
        // dosya yoluna karismiyor.
        var dosyaAdi = $"{Guid.CreateVersion7()}{uzanti.ToLowerInvariant()}";
        var goreliYol = Path.Combine(altKlasor, dosyaAdi).Replace('\\', '/');

        var kok = KokKlasor();
        var hedefKlasor = Path.Combine(kok, altKlasor);
        Directory.CreateDirectory(hedefKlasor);

        var tamYol = Path.Combine(hedefKlasor, dosyaAdi);

        // Son savunma: uretilen yol gercekten kokun altinda mi. Ad burada
        // uretildigi icin beklenmeyen bir durum, ama yol birlestirmede bir
        // hata olursa sessizce kok disina yazmak yerine burada patlamali.
        KokunAltindaOlmali(kok, tamYol);

        await using (var hedef = new FileStream(
            tamYol, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await icerik.CopyToAsync(hedef, cancellationToken);
        }

        var boyut = new FileInfo(tamYol).Length;

        logger.LogInformation(
            "Dosya kaydedildi: {Yol} ({Boyut} bayt)", goreliYol, boyut);

        return new StoredFile(dosyaAdi, goreliYol, boyut);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var kok = KokKlasor();
        var tamYol = Path.GetFullPath(Path.Combine(kok, relativePath));

        // Silme isteginde yol veritabanindan geliyor ama yine de dogrulaniyor:
        // kayit bozulmus ya da elle degistirilmisse "../" iceren bir deger
        // depolama kokunun disindaki bir dosyayi sildirebilirdi.
        KokunAltindaOlmali(kok, tamYol);

        if (File.Exists(tamYol))
        {
            File.Delete(tamYol);
            logger.LogInformation("Dosya silindi: {Yol}", relativePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var kok = KokKlasor();
        var tamYol = Path.GetFullPath(Path.Combine(kok, relativePath));

        // Yol veritabanindan geliyor ama yine de dogrulaniyor: kayit bozulmus
        // ya da elle degistirilmisse "../" iceren bir deger depolama kokunun
        // disindaki bir dosyayi (ornegin appsettings.json) OKUTABILIRDI.
        // Silmede aynisi yapiliyor; okumada atlanmasi daha tehlikeli olurdu
        // cunku sonucu dogrudan istemciye gidiyor.
        KokunAltindaOlmali(kok, tamYol);

        if (!File.Exists(tamYol))
            return Task.FromResult<Stream?>(null);

        Stream akis = new FileStream(
            tamYol, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        return Task.FromResult<Stream?>(akis);
    }

    private string KokKlasor()
    {
        var kok = Path.IsPathRooted(_options.Path)
            ? _options.Path
            : Path.Combine(AppContext.BaseDirectory, _options.Path);

        Directory.CreateDirectory(kok);

        return Path.GetFullPath(kok);
    }

    private static void KokunAltindaOlmali(string kok, string tamYol)
    {
        var normalKok = Path.GetFullPath(kok)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!Path.GetFullPath(tamYol).StartsWith(normalKok, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Dosya yolu depolama klasorunun disina cikiyor.");
    }
}
