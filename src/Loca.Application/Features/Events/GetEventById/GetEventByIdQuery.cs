using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Constants;
using Loca.Domain.Enums;
using MediatR;

namespace Loca.Application.Features.Events.GetEventById;

public sealed record GetEventByIdQuery(Guid EventId) : IRequest<Result<EventDetail>>;

internal sealed class GetEventByIdQueryHandler(
    IEventQueries queries,
    ICurrentUserService currentUser)
    : IRequestHandler<GetEventByIdQuery, Result<EventDetail>>
{
    /// <summary>
    /// Detay sayfasinin herkese acik oldugu durumlar.
    /// </summary>
    /// <remarks>
    /// Listedeki kumeden bir madde fazla: iptal edilmis etkinlik.
    /// </remarks>
    private static readonly EventStatus[] PublicStatuses =
    [
        EventStatus.Published,
        EventStatus.SalesOpen,
        EventStatus.SalesClosed,
        EventStatus.Completed,
        // Iptal edilen etkinlik DETAYDA gorunur: bilet almis kullanici
        // "etkinligim ne oldu" diye baktiginda iptal gerekcesini gormeli.
        // Listede gorunmuyor, detayda goruyor.
        EventStatus.Cancelled
    ];

    public async Task<Result<EventDetail>> Handle(
        GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detay = await queries.GetDetailAsync(request.EventId, cancellationToken);

        if (detay is null)
            return Result.Failure<EventDetail>(EventErrors.NotFound);

        if (PublicStatuses.Contains(detay.Status))
            return Result.Success(detay);

        // Taslak, onay bekleyen ve askidaki etkinligi yalnizca sahibi ve
        // admin gorebilir. Yabanciya 404 doniyor, 403 degil: burada kaydin
        // varligini dogrulamanin bir faydasi yok, aksine henuz duyurulmamis
        // bir etkinligin var oldugu bilgisini sizdirirdi.
        var yetkili =
            currentUser.IsInRole(RoleNames.Admin) ||
            (currentUser.UserId is { } userId && userId == detay.OrganizerId);

        return yetkili
            ? Result.Success(detay)
            : Result.Failure<EventDetail>(EventErrors.NotFound);
    }
}
