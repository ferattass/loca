using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Files.GetFile;

/// <param name="Content">
/// Dosya icerigi. Cagiran dispose etmekle yukumlu; bayt dizisi yerine akis
/// donuyor cunku bes megabaytlik bir afis her istekte bellege
/// kopyalanmamali.
/// </param>
/// <param name="IsPublic">
/// Onbelleklenebilirligi belirliyor. <b>Ozel belge paylasimli onbellege
/// YAZILAMAZ:</b> tek bir "public, max-age" basligi, araya giren bir vekil
/// veya CDN'in sahne kira sozlesmesini saklayip baska bir istege servis
/// etmesi demek olurdu.
/// </param>
public sealed record DosyaIcerigi(
    Stream Content,
    string ContentType,
    string FileName,
    bool IsPublic);

/// <summary>
/// Yuklenen dosyayi servis eder.
/// </summary>
/// <remarks>
/// <b>Boyle bir uc yoktu.</b> Afis ve mekan gorseli yukleniyor, kaydi
/// tutuluyor ama hicbir yerden okunamiyordu; arayuzdeki etkinlik karti
/// "gorseli servis eden bir uc yok" notuyla dekoratif bir blok
/// cizmek zorunda kalmisti.
///
/// <para>
/// Erisim <c>UploadedFile.IsPublic</c> alanina bakiyor: afis herkese acik,
/// belge degil. Kapali dosyaya yalnizca yukleyeni ve onay ekibi
/// erisebiliyor. Kimligin tahmin edilemez olmasi (Guid) tek basina koruma
/// sayilmadi — adres bir kez paylasildiginda kalici olarak acilirdi.
/// </para>
/// </remarks>
public sealed record GetFileQuery(Guid Id) : IRequest<Result<DosyaIcerigi>>;

internal static class GetFileErrors
{
    internal static readonly Error NotFound =
        Error.NotFound("File.NotFound", "Dosya bulunamadi.");

    /// <remarks>
    /// 403 degil <b>404</b> donmesi bilincli: 403, "bu kimlikte bir dosya
    /// var ama goremezsin" demek olurdu ve var olmayan kimliklerle
    /// deneyerek hangi belgelerin var oldugu ogrenilebilirdi.
    /// </remarks>
    internal static readonly Error Forbidden =
        Error.NotFound("File.NotFound", "Dosya bulunamadi.");
}

internal sealed class GetFileQueryHandler(
    IUploadedFileRepository files,
    IFileStorageService storage,
    ICurrentUserService currentUser)
    : IRequestHandler<GetFileQuery, Result<DosyaIcerigi>>
{
    public async Task<Result<DosyaIcerigi>> Handle(
        GetFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kayit = await files.GetByIdAsync(request.Id, cancellationToken);

        if (kayit is null)
            return Result.Failure<DosyaIcerigi>(GetFileErrors.NotFound);

        if (!kayit.IsPublic && !Gorebilir(kayit.UploadedByUserId))
            return Result.Failure<DosyaIcerigi>(GetFileErrors.Forbidden);

        var icerik = await storage.OpenReadAsync(kayit.RelativePath, cancellationToken);

        // Kayit var ama dosya yok: disk temizlenmis ya da kopyalanan bir
        // veritabaniyla calisiliyor. Cokmek yerine 404.
        if (icerik is null)
            return Result.Failure<DosyaIcerigi>(GetFileErrors.NotFound);

        return Result.Success(new DosyaIcerigi(
            icerik,
            // Saklanan ContentType istemciden gelmisti; icerik dogrulanirken
            // uzanti zaten imzayla eslestirildi, bu yuzden tur uzantidan
            // uretiliyor. Istemcinin bildirdigi basligi geri yansitmak,
            // yuklerken reddettigimiz bilgiye servis ederken guvenmek olurdu.
            TurdenBaslik(Path.GetExtension(kayit.FileName)),
            kayit.OriginalFileName,
            kayit.IsPublic));
    }

    private bool Gorebilir(Guid? yukleyen) =>
        currentUser.IsInRole(RoleNames.Admin)
        || currentUser.IsInRole(RoleNames.Moderator)
        || (currentUser.UserId is { } kim && yukleyen == kim);

    private static string TurdenBaslik(string uzanti) => uzanti.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        // Taninmayan uzanti buraya gelmemeli (yuklemede dogrulandi); yine de
        // tarayiciya "bunu calistirma, indir" diyen tur donuyor.
        _ => "application/octet-stream",
    };
}
