using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.SetEventPoster;

/// <remarks>
/// Dosyanin kendisi Gun 4'te yazilan <c>POST /files</c> ucundan yuklenir ve
/// tur dogrulamasi orada, icerigin ilk baytlarina bakilarak yapilir. Burada
/// yalnizca yuklenmis dosya etkinlige baglaniyor — iki isin ayrilmasi,
/// ayni gorselin birden fazla yere baglanmasini da mumkun kiliyor.
/// </remarks>
public sealed record SetEventPosterCommand(Guid EventId, Guid? PosterFileId) : IRequest<Result>;

internal sealed class SetEventPosterCommandHandler(
    IEventRepository events,
    IUploadedFileRepository files,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<SetEventPosterCommandHandler> logger)
    : IRequestHandler<SetEventPosterCommand, Result>
{
    public async Task<Result> Handle(
        SetEventPosterCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        if (request.PosterFileId is { } fileId)
        {
            // Var olmayan bir dosya kimligi baglanirsa yabanci anahtar hatasi
            // 500'e cevrilirdi; burada kontrol edilip 404 donuyor.
            var dosya = await files.GetByIdAsync(fileId, cancellationToken);

            if (dosya is null)
            {
                return Result.Failure(Error.NotFound(
                    "Event.PosterNotFound", "Afis dosyasi bulunamadi."));
            }
        }

        ev.SetPoster(request.PosterFileId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Etkinlik afisi guncellendi. EtkinlikId: {EtkinlikId}, DosyaId: {DosyaId}",
            ev.Id,
            request.PosterFileId);

        return Result.Success();
    }
}
