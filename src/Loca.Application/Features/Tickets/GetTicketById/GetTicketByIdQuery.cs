using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Tickets.Common;
using MediatR;

namespace Loca.Application.Features.Tickets.GetTicketById;

public sealed record GetTicketByIdQuery(Guid Id) : IRequest<Result<TicketDetail>>;

internal sealed class GetTicketByIdQueryHandler(
    ITicketQueries queries,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTicketByIdQuery, Result<TicketDetail>>
{
    public async Task<Result<TicketDetail>> Handle(
        GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<TicketDetail>(TicketErrors.Unauthenticated);

        // Sahiplik ayri bir kontrol degil, sorgunun kendisinde. Once bilet
        // okunup sonra sahibi karsilastirilsaydi baskasinin QR kodu bir an
        // icin bellege alinmis olurdu.
        var bilet = await queries.GetByIdAsync(request.Id, userId, cancellationToken);

        return bilet is null
            ? Result.Failure<TicketDetail>(TicketErrors.NotFound)
            : Result.Success(bilet);
    }
}
