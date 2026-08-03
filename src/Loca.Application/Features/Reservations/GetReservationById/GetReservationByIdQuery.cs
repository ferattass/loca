using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Reservations.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Reservations.GetReservationById;

public sealed record GetReservationByIdQuery(Guid Id) : IRequest<Result<ReservationDetail>>;

internal sealed class GetReservationByIdQueryHandler(
    IReservationRepository reservations,
    IReservationQueries queries,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
    : IRequestHandler<GetReservationByIdQuery, Result<ReservationDetail>>
{
    public async Task<Result<ReservationDetail>> Handle(
        GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is null)
            return Result.Failure<ReservationDetail>(ReservationErrors.Unauthenticated);

        // Sahiplik once entity uzerinden kontrol ediliyor, sonra projeksiyon
        // okunuyor. Ters sirada yapilsaydi baskasinin rezervasyonunun tum
        // ayrintilari once belleğe alinir, sonra atilirdi — hata yolunda
        // sizmaya acik bir tasarim.
        var rezervasyon = await reservations.GetAggregateAsync(request.Id, cancellationToken);

        if (rezervasyon is null)
            return Result.Failure<ReservationDetail>(ReservationErrors.NotFound);

        if (!Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), rezervasyon))
            return Result.Failure<ReservationDetail>(ReservationErrors.NotOwner);

        var detay = await queries.GetDetailAsync(request.Id, clock.UtcNow, cancellationToken);

        return detay is null
            ? Result.Failure<ReservationDetail>(ReservationErrors.NotFound)
            : Result.Success(detay);
    }
}
