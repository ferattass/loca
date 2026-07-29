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
        Guid? uploadedByUserId)
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
}
