using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Reservations.Common;
using MediatR;

namespace Loca.Application.Features.Reservations.GetMyReservations;

/// <summary>
/// Giris yapmis kullanicinin rezervasyonlari.
/// </summary>
/// <remarks>
/// Kullanici kimligi istekten DEGIL token'dan okunuyor. Parametre olarak
/// alinsaydi herkes baskasinin kimligini yazip rezervasyonlarini
/// listeleyebilirdi — yetkilendirmenin en sik atlanan noktasi.
/// </remarks>
public sealed record GetMyReservationsQuery : IRequest<Result<IReadOnlyList<ReservationListItem>>>;

internal sealed class GetMyReservationsQueryHandler(
    IReservationQueries queries,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
    : IRequestHandler<GetMyReservationsQuery, Result<IReadOnlyList<ReservationListItem>>>
{
    public async Task<Result<IReadOnlyList<ReservationListItem>>> Handle(
        GetMyReservationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<IReadOnlyList<ReservationListItem>>(
                ReservationErrors.Unauthenticated);

        var kayitlar = await queries.GetByUserAsync(userId, clock.UtcNow, cancellationToken);

        return Result.Success(kayitlar);
    }
}
