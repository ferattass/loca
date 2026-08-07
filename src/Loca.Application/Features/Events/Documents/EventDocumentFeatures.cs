using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.Documents;

/// <param name="FileUrl">
/// Belgeye erisim adresi. <b>Yalnizca sahibine ve onay ekibine donuyor:</b>
/// kira sozlesmesi imza ve ticari kosul tasiyor, vitrinde gorunmesi icin
/// hicbir sebep yok.
/// </param>
public sealed record EtkinlikBelgesi(
    Guid Id,
    EventDocumentKind Kind,
    string OriginalFileName,
    string FileUrl,
    long SizeInBytes,
    string? Note,
    DateTime UploadedAt);

/// <summary>Etkinligin belgelerini okuma tarafi.</summary>
public interface IEventDocumentQueries
{
    Task<IReadOnlyList<EtkinlikBelgesi>> GetByEventAsync(
        Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Etkinlige sahne sozlesmesi eklenmis mi.</summary>
    /// <remarks>
    /// Ayri bir metot, listeyi cekip saymak yerine: onaya gonderme yolunda
    /// yalnizca bu cevap gerekiyor ve belgelerin tamami (dosya adlari,
    /// notlar) okunmadan sorulabiliyor.
    /// </remarks>
    Task<bool> HasVenueContractAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Etkinlige belge ekler.</summary>
public sealed record AddEventDocumentCommand(
    Guid EventId,
    Guid UploadedFileId,
    EventDocumentKind Kind,
    string? Note) : IRequest<Result<Guid>>;

internal sealed class AddEventDocumentCommandValidator
    : AbstractValidator<AddEventDocumentCommand>
{
    public AddEventDocumentCommandValidator()
    {
        RuleFor(komut => komut.EventId).NotEmpty();
        RuleFor(komut => komut.UploadedFileId).NotEmpty().WithMessage("Once dosya yuklenmeli.");
        RuleFor(komut => komut.Kind).IsInEnum().WithMessage("Gecersiz belge turu.");
        RuleFor(komut => komut.Note).MaximumLength(300);
    }
}

internal sealed class AddEventDocumentCommandHandler(
    IEventRepository events,
    IEventDocumentRepository documents,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<AddEventDocumentCommandHandler> logger)
    : IRequestHandler<AddEventDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        AddEventDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } kullaniciId)
            return Result.Failure<Guid>(EventErrors.Unauthenticated);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure<Guid>(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure<Guid>(hata);

        // Yayindaki etkinligin belgesi degistirilemiyor: onay o belgelere
        // bakilarak verildi, sonradan degistirilebilseydi onayin dayanagi
        // ortadan kalkardi.
        if (ev.Status is not (EventStatus.Draft or EventStatus.PendingApproval))
            return Result.Failure<Guid>(EventErrors.DocumentsLocked);

        var belge = new EventDocument(
            ev.Id, request.UploadedFileId, request.Kind, kullaniciId, request.Note);

        documents.Add(belge);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Etkinlige belge eklendi. EtkinlikId: {EtkinlikId}, Tur: {Tur}, BelgeId: {BelgeId}",
            ev.Id,
            request.Kind,
            belge.Id);

        return Result.Success(belge.Id);
    }
}

/// <summary>Etkinligin belgelerini listeler.</summary>
/// <remarks>
/// Gorme yetkisi ikili: etkinligin sahibi ve onay ekibi. Herkese acik
/// olsaydi kira sozlesmesindeki tutar ve imza disariya cikardi.
/// </remarks>
public sealed record GetEventDocumentsQuery(Guid EventId)
    : IRequest<Result<IReadOnlyList<EtkinlikBelgesi>>>;

internal sealed class GetEventDocumentsQueryHandler(
    IEventRepository events,
    IEventDocumentQueries queries,
    ICurrentUserService currentUser)
    : IRequestHandler<GetEventDocumentsQuery, Result<IReadOnlyList<EtkinlikBelgesi>>>
{
    public async Task<Result<IReadOnlyList<EtkinlikBelgesi>>> Handle(
        GetEventDocumentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure<IReadOnlyList<EtkinlikBelgesi>>(EventErrors.NotFound);

        var onayEkibi = currentUser.IsInRole(RoleNames.Admin)
            || currentUser.IsInRole(RoleNames.Moderator);

        if (!onayEkibi && EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure<IReadOnlyList<EtkinlikBelgesi>>(hata);

        return Result.Success(await queries.GetByEventAsync(ev.Id, cancellationToken));
    }
}

/// <summary>Belgeyi siler.</summary>
public sealed record DeleteEventDocumentCommand(Guid EventId, Guid DocumentId) : IRequest<Result>;

internal sealed class DeleteEventDocumentCommandHandler(
    IEventRepository events,
    IEventDocumentRepository documents,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<DeleteEventDocumentCommandHandler> logger)
    : IRequestHandler<DeleteEventDocumentCommand, Result>
{
    public async Task<Result> Handle(
        DeleteEventDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetByIdAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        if (ev.Status is not (EventStatus.Draft or EventStatus.PendingApproval))
            return Result.Failure(EventErrors.DocumentsLocked);

        var belge = await documents.GetAsync(request.DocumentId, cancellationToken);

        // Belgenin BU etkinlige ait oldugu ayrica dogrulaniyor: yol
        // parametresi istemciden geliyor ve baska bir etkinligin belgesinin
        // kimligi yazilabilirdi.
        if (belge is null || belge.EventId != ev.Id)
            return Result.Failure(EventErrors.DocumentNotFound);

        documents.Remove(belge);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Etkinlik belgesi silindi. EtkinlikId: {EtkinlikId}, BelgeId: {BelgeId}",
            ev.Id,
            belge.Id);

        return Result.Success();
    }
}
