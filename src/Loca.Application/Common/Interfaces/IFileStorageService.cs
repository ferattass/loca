namespace Loca.Application.Common.Interfaces;

/// <param name="FileName">Diske yazilan guvenli ad: <c>Guid</c> + uzanti.</param>
/// <param name="RelativePath">Depolama kokune gore yol. Mutlak yol disari cikmaz.</param>
public sealed record StoredFile(string FileName, string RelativePath, long SizeInBytes);

/// <summary>
/// Dosya saklama. Su an yerel disk; ilerde bulut saglayicisina gecilirse
/// yalnizca bu arayuzun uygulamasi degisir.
/// </summary>
public interface IFileStorageService
{
    /// <param name="uzanti">Nokta ile baslayan, dogrulanmis uzanti.</param>
    /// <remarks>
    /// Dosya adi cagirandan alinmaz, burada uretilir: kullanicidan gelen ad
    /// <c>../../appsettings.json</c> gibi bir yol icerebilir.
    /// </remarks>
    Task<StoredFile> SaveAsync(
        Stream icerik, string uzanti, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Dosyayi okumak icin acar; yoksa <c>null</c>.</summary>
    /// <remarks>
    /// Stream donuyor, bayt dizisi degil: bes megabaytlik bir afis her
    /// istekte belege kopyalanmamali. Cagiran tarafin dispose etmesi
    /// gerekiyor.
    /// </remarks>
    Task<Stream?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default);
}
