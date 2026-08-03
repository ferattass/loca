using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Reservations.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Reservations.CancelReservation;

/// <summary>
/// Kullanici rezervasyondan vazgecti; koltuklar hemen serbest kalir.
/// </summary>
/// <remarks>
/// Koltuklarin kilit suresinin dolmasi beklenmiyor: vazgectigi belli olan
/// bir kullanici yuzunden koltugun on dakika daha bloke kalmasinin
/// karsiligi yok.
/// </remarks>
public sealed record CancelReservationCommand(Guid Id) : IRequest<Result>;

internal sealed class CancelReservationCommandHandler(
    IReservationRepository reservations,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<CancelReservationCommandHandler> logger)
    : IRequestHandler<CancelReservationCommand, Result>
{
    public async Task<Result> Handle(
        CancelReservationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is null)
            return Result.Failure(ReservationErrors.Unauthenticated);

        var rezervasyon = await reservations.GetAggregateAsync(request.Id, cancellationToken);

        if (rezervasyon is null)
            return Result.Failure(ReservationErrors.NotFound);

        if (!Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), rezervasyon))
            return Result.Failure(ReservationErrors.NotOwner);

        var utcNow = clock.UtcNow;

        // Durum gecisi domain'de: onaylanmis rezervasyonun iptali iade
        // gerektirir ve buradan yapilamaz. Ihlal DomainException → 409.
        rezervasyon.Cancel(utcNow);

        // Yalnizca HÂLÂ bu rezervasyona bagli koltuklar donuyor. Kilit suresi
        // dolup koltuk baskasina gectiyse artik ReservationId bu kayda isaret
        // etmez ve serbest birakilmaz — aksi hâlde iptal, baskasinin
        // koltugunu elinden alirdi.
        var koltuklar = await reservations.GetSeatsOfReservationAsync(
            rezervasyon.Id, cancellationToken);

        foreach (var koltuk in koltuklar)
            koltuk.Release();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Rezervasyon iptal edildi. RezervasyonId: {RezervasyonId}, SerbestKalanKoltuk: {KoltukSayisi}",
            rezervasyon.Id,
            koltuklar.Count);

        return Result.Success();
    }
}
