using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Yuklenen dosyanin kaydi. Dosyanin kendisi diskte, bilgisi burada durur.
/// </summary>
/// <remarks>
/// Dosya adi kullanicidan gelen adla saklanmaz. Iki sebep var: ayni adla
/// yuklenen ikinci dosya birincinin uzerine yazar, ve kullanicidan gelen ad
/// <c>../../appsettings.json</c> gibi bir yol icerebilir (path traversal).
/// Diske yazilan ad her zaman <c>Guid</c> + uzanti; kullanicinin verdigi ad
/// yalnizca gosterim icin <see cref="OriginalFileName"/> alaninda tutulur.
/// </remarks>
public sealed class UploadedFile : BaseEntity
{
    private UploadedFile()
    {
        FileName = string.Empty;
        OriginalFileName = string.Empty;
        ContentType = string.Empty;
        RelativePath = string.Empty;
    }

    public UploadedFile(
        string fileName,
        string originalFileName,
        string contentType,
        long sizeInBytes,
        string relativePath,
        Guid? uploadedByUserId,
        bool isPublic = true)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Dosya adi bos olamaz.");

        if (sizeInBytes <= 0)
            throw new DomainException("Dosya boyutu sifirdan buyuk olmali.");

        FileName = fileName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        RelativePath = relativePath;
        UploadedByUserId = uploadedByUserId;
        IsPublic = isPublic;
    }

    /// <summary>Diskteki guvenli ad: <c>Guid</c> + uzanti.</summary>
    public string FileName { get; private set; }

    /// <summary>Kullanicinin yukledigi dosyanin adi. Yalnizca gosterim icin.</summary>
    public string OriginalFileName { get; private set; }

    public string ContentType { get; private set; }
    public long SizeInBytes { get; private set; }

    /// <summary>Depolama kokune gore goreli yol. Mutlak yol saklanmaz.</summary>
    public string RelativePath { get; private set; }

    public Guid? UploadedByUserId { get; private set; }

    /// <summary>
    /// Dosya herkese acik mi.
    /// </summary>
    /// <remarks>
    /// <b>Afis ile sozlesme ayni depoda ama ayni gorunurlukte degil.</b>
    /// Afisin vitrinde gorunmesi gerekiyor; sahne kira sozlesmesinin
    /// gorunmemesi. Ayrim dosyanin turune bakilarak tahmin edilemez —
    /// ikisi de PNG olabilir — bu yuzden yukleme aninda isaretleniyor:
    /// gorsel ucu acik, belge ucu kapali kaydediyor.
    ///
    /// <para>
    /// Kapali dosyaya yalnizca yukleyeni ve onay ekibi erisebiliyor.
    /// "Kimse kimligi tahmin edemez" (Guid) tek basina bir koruma degil:
    /// adres bir kez paylasildiginda kalici olarak acik hâle gelirdi.
    /// </para>
    /// </remarks>
    public bool IsPublic { get; private set; } = true;
}
