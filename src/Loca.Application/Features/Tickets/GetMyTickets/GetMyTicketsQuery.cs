using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Tickets.Common;
using MediatR;

namespace Loca.Application.Features.Tickets.GetMyTickets;

/// <summary>
/// Giris yapmis kullanicinin biletleri.
/// </summary>
/// <remarks>
/// Kullanici kimligi istekten DEGIL token'dan okunuyor: parametre olarak
/// alinsaydi herkes baskasinin kimligini yazip biletlerini — QR kodlariyla
/// birlikte — listeleyebilirdi. Bilet QR'i kapida gecerli olan sey oldugu
/// icin bu sizinti dogrudan bedava girise donusurdu.
/// </remarks>
public sealed record GetMyTicketsQuery(Guid? ReservationId = null)
    : IRequest<Result<IReadOnlyList<TicketDetail>>>;

internal sealed class GetMyTicketsQueryHandler(
    ITicketQueries queries,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
    : IRequestHandler<GetMyTicketsQuery, Result<IReadOnlyList<TicketDetail>>>
{
    public async Task<Result<IReadOnlyList<TicketDetail>>> Handle(
        GetMyTicketsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<IReadOnlyList<TicketDetail>>(TicketErrors.Unauthenticated);

        var biletler = await queries.GetByUserAsync(
            userId, request.ReservationId, clock.UtcNow, cancellationToken);

        return Result.Success(biletler);
    }
}
