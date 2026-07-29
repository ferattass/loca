using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Venues.SetVenueImage;

/// <param name="ImageFileId">Bos verilirse mevcut gorsel baglantisi kaldirilir.</param>
public sealed record SetVenueImageCommand(Guid Id, Guid? ImageFileId) : IRequest<Result>;

internal sealed class SetVenueImageCommandHandler(
    IVenueRepository venues,
    IUploadedFileRepository files,
    IUnitOfWork unitOfWork,
    ILogger<SetVenueImageCommandHandler> logger)
    : IRequestHandler<SetVenueImageCommand, Result>
{
    public async Task<Result> Handle(
        SetVenueImageCommand request, CancellationToken cancellationToken)
    {
        var venue = await venues.GetByIdAsync(request.Id, cancellationToken);

        if (venue is null)
            return Result.Failure(VenueErrors.NotFound);

        // Var olmayan bir dosya kimligi baglanirsa yabanci anahtar hatasi
        // veritabanindan donerdi; burada anlamli mesajla kesiliyor.
        if (request.ImageFileId is { } dosyaId &&
            await files.GetByIdAsync(dosyaId, cancellationToken) is null)
        {
            return Result.Failure(VenueErrors.ImageNotFound);
        }

        venue.SetImage(request.ImageFileId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Mekan gorseli guncellendi. MekanId: {MekanId}, DosyaId: {DosyaId}",
            venue.Id, request.ImageFileId);

        return Result.Success();
    }
}
