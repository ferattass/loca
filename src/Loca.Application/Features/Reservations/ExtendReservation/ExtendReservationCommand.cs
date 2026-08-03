using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Reservations.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Reservations.ExtendReservation;

/// <summary>
/// Kilit suresini bir kez uzatir (+5 dakika).
/// </summary>
/// <remarks>
/// Sinirsiz uzatma, koltugu satin almadan istedigi kadar tutmak demek
/// olurdu: tek bir kullanici salonun tamamini kilitleyip satisi durdurabilirdi.
/// Hak bir kez ve sabit sure.
/// </remarks>
public sealed record ExtendReservationCommand(Guid Id) : IRequest<Result<ReservationDetail>>;

internal sealed class ExtendReservationCommandHandler(
    IReservationRepository reservations,
    IReservationQueries queries,
    IUnitOfWork unitOfWork,
    IReservationPolicy policy,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<ExtendReservationCommandHandler> logger)
    : IRequestHandler<ExtendReservationCommand, Result<ReservationDetail>>
{
    public async Task<Result<ReservationDetail>> Handle(
        ExtendReservationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is null)
            return Result.Failure<ReservationDetail>(ReservationErrors.Unauthenticated);

        var rezervasyon = await reservations.GetAggregateAsync(request.Id, cancellationToken);

        if (rezervasyon is null)
            return Result.Failure<ReservationDetail>(ReservationErrors.NotFound);

        if (!Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), rezervasyon))
            return Result.Failure<ReservationDetail>(ReservationErrors.NotOwner);

        var utcNow = clock.UtcNow;

        // "Bir kez uzatilir", "suresi dolmus uzatilamaz" ve durum kontrolu
        // domain'de. Ihlal DomainException olarak cikar ve 409'a cevrilir.
        var yeniBitis = rezervasyon.Extend(utcNow, policy.ExtendDuration);

        // KOLTUKLARIN KILIDI DE UZATILMALI. Yalnizca rezervasyon uzatilsaydi
        // koltuklarin LockedUntilUtc'si eski degerde kalir, koltuk plani
        // ekraninda "suresi dolmus kilit" bos gorunur ve baskasi ayni
        // koltugu secmeye calisirdi.
        var koltuklar = await reservations.GetSeatsOfReservationAsync(
            rezervasyon.Id, cancellationToken);

        foreach (var koltuk in koltuklar)
            koltuk.ExtendLock(utcNow, policy.ExtendDuration);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Rezervasyon uzatildi. RezervasyonId: {RezervasyonId}, YeniBitis: {YeniBitis}",
            rezervasyon.Id,
            yeniBitis);

        var detay = await queries.GetDetailAsync(rezervasyon.Id, utcNow, cancellationToken);

        return detay is null
            ? Result.Failure<ReservationDetail>(ReservationErrors.NotFound)
            : Result.Success(detay);
    }
}
