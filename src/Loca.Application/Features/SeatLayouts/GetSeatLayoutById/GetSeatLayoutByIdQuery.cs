using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.SeatLayouts.GetSeatLayoutById;

/// <param name="KoltuklarlaBirlikte">
/// Gorsel plan icin koltuklar da doner. Varsayilan olarak donmez;
/// 600 koltuklu bir planda gereksiz yere yuzlerce satir tasinirdi.
/// </param>
public sealed record GetSeatLayoutByIdQuery(Guid Id, bool KoltuklarlaBirlikte)
    : IRequest<Result<SeatLayoutResponse>>;

internal sealed class GetSeatLayoutByIdQueryHandler(ISeatLayoutRepository seatLayouts)
    : IRequestHandler<GetSeatLayoutByIdQuery, Result<SeatLayoutResponse>>
{
    public async Task<Result<SeatLayoutResponse>> Handle(
        GetSeatLayoutByIdQuery request, CancellationToken cancellationToken)
    {
        var layout = await seatLayouts.GetByIdAsync(
            request.Id, request.KoltuklarlaBirlikte, cancellationToken);

        if (layout is null)
            return Result.Failure<SeatLayoutResponse>(SeatLayoutErrors.NotFound);

        // Koltuklar yuklenmediyse sayim ayri sorguyla alinir; yuklendiyse
        // bellekte sayilir, ikinci bir sorgu atilmaz.
        var toplamKoltuk = request.KoltuklarlaBirlikte
            ? layout.Sections.Sum(section => section.Seats.Count)
            : await seatLayouts.CountSeatsAsync(layout.Id, cancellationToken);

        var bolumler = layout.Sections
            .OrderBy(section => section.DisplayOrder)
            .ThenBy(section => section.Name)
            .Select(section => new SeatSectionResponse(
                section.Id,
                section.Name,
                section.DisplayOrder,
                [.. section.Seats
                    .OrderBy(seat => seat.RowLabel)
                    .ThenBy(seat => seat.SeatNumber)
                    .Select(seat => new SeatResponse(
                        seat.Id,
                        seat.RowLabel,
                        seat.SeatNumber,
                        seat.Label,
                        seat.PositionX,
                        seat.PositionY,
                        seat.IsActive))]))
            .ToList();

        return Result.Success(new SeatLayoutResponse(
            layout.Id,
            layout.HallId,
            layout.Hall?.Name ?? string.Empty,
            layout.Hall?.Capacity ?? 0,
            layout.Name,
            layout.Description,
            layout.IsActive,
            toplamKoltuk,
            bolumler));
    }
}
