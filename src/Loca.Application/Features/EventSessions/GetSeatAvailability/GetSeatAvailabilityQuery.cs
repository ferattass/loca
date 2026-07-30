using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using MediatR;

namespace Loca.Application.Features.EventSessions.GetSeatAvailability;

/// <summary>
/// Bir oturumun koltuk durumlari. Gorsel koltuk planinin veri kaynagi.
/// </summary>
/// <remarks>
/// Anonim erisime acik: kullanici giris yapmadan da salonun ne kadar dolu
/// oldugunu gormeli. Giris yapmis kullanicinin kendi kilitleri
/// <c>isLockedByMe</c> ile isaretlenir.
/// </remarks>
public sealed record GetSeatAvailabilityQuery(Guid EventSessionId)
    : IRequest<Result<SeatAvailability>>;

internal sealed class GetSeatAvailabilityQueryHandler(
    IEventQueries queries,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
    : IRequestHandler<GetSeatAvailabilityQuery, Result<SeatAvailability>>
{
    public async Task<Result<SeatAvailability>> Handle(
        GetSeatAvailabilityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var durum = await queries.GetSeatAvailabilityAsync(
            request.EventSessionId,
            currentUser.UserId,
            clock.UtcNow,
            cancellationToken);

        if (durum is null)
            return Result.Failure<SeatAvailability>(EventErrors.SessionNotFound);

        // Bos bolum listesi "oturum var ama koltuk uretilmemis" demek.
        // Bos dizi dondurmek istemcide "salon tamamen dolu" gibi gorunurdu;
        // ayri bir hata kodu ile ne yapilmasi gerektigi soyleniyor.
        if (durum.Sections.Count == 0)
            return Result.Failure<SeatAvailability>(EventErrors.SeatsNotGenerated);

        return Result.Success(durum);
    }
}
