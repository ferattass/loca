using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using MediatR;

namespace Loca.Application.Features.TicketTypes.GetTicketTypes;

public sealed record GetTicketTypesQuery(Guid EventId)
    : IRequest<Result<IReadOnlyList<TicketTypeItem>>>;

internal sealed class GetTicketTypesQueryHandler(IEventQueries queries)
    : IRequestHandler<GetTicketTypesQuery, Result<IReadOnlyList<TicketTypeItem>>>
{
    public async Task<Result<IReadOnlyList<TicketTypeItem>>> Handle(
        GetTicketTypesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var turler = await queries.GetTicketTypesAsync(request.EventId, cancellationToken);

        return Result.Success(turler);
    }
}
