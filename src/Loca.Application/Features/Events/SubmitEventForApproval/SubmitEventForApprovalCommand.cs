using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.SubmitEventForApproval;

/// <summary>
/// Etkinligi admin onayina gonderir: Draft → PendingApproval.
/// </summary>
/// <remarks>
/// <b>Kilavuzun uc tablosundan sapma — bilincli.</b> Kilavuz
/// <c>POST /events/{id}/publish</c> ucunu dogrudan EventOwner'a veriyor ve
/// ara bir onay adimi gostermiyor. Ancak sartnamenin etkinlik durumlari
/// arasinda <c>PendingApproval</c> var ve admin yetkileri arasinda
/// "uygunsuz etkinlikleri pasif hâle getirebilir" maddesi bulunuyor;
/// onay adimi olmadan <c>PendingApproval</c> durumunun karsiligi kalmaz ve
/// moderasyon ancak etkinlik yayina cikip bilet satildiktan SONRA
/// devreye girer.
///
/// <para>
/// Bu yuzden akis iki uca bolundu: organizator onaya gonderir (bu uc),
/// admin yayina alir. Domain katmanindaki <c>Event.Publish</c> zaten
/// yalnizca <c>PendingApproval</c> durumundan calisiyor ve bu kural
/// birim testiyle bagli.
/// </para>
/// </remarks>
public sealed record SubmitEventForApprovalCommand(Guid EventId) : IRequest<Result>;

internal sealed class SubmitEventForApprovalCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<SubmitEventForApprovalCommandHandler> logger)
    : IRequestHandler<SubmitEventForApprovalCommand, Result>
{
    public async Task<Result> Handle(
        SubmitEventForApprovalCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Yayin on kosullari oturum ve bilet turu koleksiyonlarina bakiyor;
        // aggregate eksik yuklenirse "oturum yok" diye yanlis hata verirdi.
        var ev = await events.GetAggregateAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure(EventErrors.NotFound);

        if (EventOwnershipGuard.Check(currentUser, ev) is { } hata)
            return Result.Failure(hata);

        // On kosullar (en az bir oturum, bir aktif bilet turu, afis) ve durum
        // gecisi domain'de; ihlal DomainException → 409.
        ev.SubmitForApproval();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Etkinlik onaya gonderildi. EtkinlikId: {EtkinlikId}", ev.Id);

        return Result.Success();
    }
}
