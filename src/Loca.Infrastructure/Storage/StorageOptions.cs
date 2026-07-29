using System.ComponentModel.DataAnnotations;

namespace Loca.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Dosyalarin yazilacagi kok klasor. Konteynerde <c>/app/uploads</c>,
    /// gelistirmede uygulama klasorunun altindaki <c>uploads</c>.
    /// </summary>
    [Required]
    public string Path { get; set; } = "uploads";

    /// <summary>
    /// Kabul edilen en buyuk dosya boyutu (MB). Sartname 5 MB diyor.
    /// </summary>
    [Range(1, 100)]
    public int MaxSizeMb { get; set; } = 5;

    public long MaxSizeInBytes => MaxSizeMb * 1024L * 1024L;
}
