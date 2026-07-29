using Loca.Application.Common.Files;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Files.UploadFile;

public sealed record UploadedFileResponse(
    Guid Id,
    string FileName,
    string OriginalFileName,
    string RelativePath,
    long SizeInBytes);

/// <param name="Content">Dosya icerigi. Okunabilir ve basa sarilabilir olmali.</param>
/// <param name="OriginalFileName">Kullanicinin verdigi ad. Yalnizca gosterim icin saklanir.</param>
public sealed record UploadFileCommand(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length) : IRequest<Result<UploadedFileResponse>>;

internal static class FileErrors
{
    internal static readonly Error Empty =
        Error.Validation("File.Empty", "Dosya bos.");

    internal static readonly Error TooLarge =
        Error.Validation("File.TooLarge", "Dosya boyutu izin verilen siniri asiyor.");

    internal static readonly Error ExtensionNotAllowed =
        Error.Validation(
            "File.ExtensionNotAllowed",
            "Yalnizca .jpg, .jpeg, .png ve .webp dosyalari yuklenebilir.");

    /// <remarks>
    /// Uzanti izinli ama icerik baska bir sey: adi degistirilmis bir dosya.
    /// Ayri bir kod donuyor cunku bu, yanlis dosya secmekten farkli — kasitli
    /// olabilir ve log'da ayirt edilebilmeli.
    /// </remarks>
    internal static readonly Error ContentMismatch =
        Error.Validation(
            "File.ContentMismatch",
            "Dosya icerigi uzantisiyla uyusmuyor. Gecerli bir gorsel yukleyin.");
}

internal sealed class UploadFileCommandHandler(
    IFileStorageService storage,
    IUploadedFileRepository files,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<UploadFileCommandHandler> logger)
    : IRequestHandler<UploadFileCommand, Result<UploadedFileResponse>>
{
    /// <summary>
    /// Boyut siniri altyapidan degil buradan okunmaz; istek boyutu sunucu
    /// tarafinda da sinirlanir. Buradaki kontrol, sinira takilan istegin
    /// anlamli bir mesajla donmesi icin.
    /// </summary>
    private const long MaxSizeInBytes = 5 * 1024 * 1024;

    public async Task<Result<UploadedFileResponse>> Handle(
        UploadFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Length <= 0)
            return Result.Failure<UploadedFileResponse>(FileErrors.Empty);

        if (request.Length > MaxSizeInBytes)
        {
            logger.LogWarning(
                "Boyut siniri asildi: {Boyut} bayt, sinir {Sinir}", request.Length, MaxSizeInBytes);

            return Result.Failure<UploadedFileResponse>(FileErrors.TooLarge);
        }

        var uzanti = Path.GetExtension(request.OriginalFileName);

        if (!ImageSignature.UzantiIzinli(uzanti))
            return Result.Failure<UploadedFileResponse>(FileErrors.ExtensionNotAllowed);

        // Icerigin ilk baytlari okunup uzantiyla karsilastiriliyor. Uzantiya ve
        // istemcinin bildirdigi MIME basligina guvenilmez: ikisini de gonderen
        // taraf belirliyor, dosyanin ne oldugunu yalnizca icerigi soyler.
        var baslik = new byte[ImageSignature.RequiredHeaderLength];
        var okunan = await request.Content.ReadAtLeastAsync(
            baslik, baslik.Length, throwOnEndOfStream: false, cancellationToken);

        var tespit = ImageSignature.Tespit(baslik.AsSpan(0, okunan));

        if (tespit is null || !ImageSignature.Eslesiyor(tespit, uzanti))
        {
            logger.LogWarning(
                "Icerik uzantiyla uyusmuyor. Uzanti: {Uzanti}, tespit: {Tespit}",
                uzanti, tespit ?? "taninmadi");

            return Result.Failure<UploadedFileResponse>(FileErrors.ContentMismatch);
        }

        // Basliga bakmak icin okunan baytlar geri sarilmali, yoksa dosya
        // eksik yazilirdi.
        request.Content.Position = 0;

        var saklanan = await storage.SaveAsync(request.Content, tespit, cancellationToken);

        var kayit = new UploadedFile(
            saklanan.FileName,
            Path.GetFileName(request.OriginalFileName),
            request.ContentType,
            saklanan.SizeInBytes,
            saklanan.RelativePath,
            currentUser.UserId);

        files.Add(kayit);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Dosya kaydi olusturuldu. DosyaId: {DosyaId}", kayit.Id);

        return Result.Success(new UploadedFileResponse(
            kayit.Id,
            kayit.FileName,
            kayit.OriginalFileName,
            kayit.RelativePath,
            kayit.SizeInBytes));
    }
}
