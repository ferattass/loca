using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.SeatLayouts.GetSeatLayoutsByHall;

public sealed record GetSeatLayoutsByHallQuery(Guid HallId)
    : IRequest<Result<IReadOnlyList<SeatLayoutListItem>>>;

internal sealed class GetSeatLayoutsByHallQueryHandler(
    ISeatLayoutRepository seatLayouts,
    IHallRepository halls)
    : IRequestHandler<GetSeatLayoutsByHallQuery, Result<IReadOnlyList<SeatLayoutListItem>>>
{
    public async Task<Result<IReadOnlyList<SeatLayoutListItem>>> Handle(
        GetSeatLayoutsByHallQuery request, CancellationToken cancellationToken)
    {
        if (await halls.GetByIdAsync(request.HallId, cancellationToken) is null)
            return Result.Failure<IReadOnlyList<SeatLayoutListItem>>(SeatLayoutErrors.HallNotFound);

        var kayitlar = await seatLayouts.GetByHallIdAsync(request.HallId, cancellationToken);

        IReadOnlyList<SeatLayoutListItem> liste =
        [
            .. kayitlar.Select(layout => new SeatLayoutListItem(
                layout.Id,
                layout.Name,
                layout.Description,
                layout.IsActive,
                layout.Sections.Count))
        ];

        return Result.Success(liste);
    }
}
