using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Constants;
using MediatR;

namespace Loca.Application.Features.Events.GetEvents;

/// <summary>
/// Etkinlik listesi.
/// </summary>
/// <remarks>
/// Filtre, arama ve cache Gun 8'e ait (yol haritasinin "yapilmayacaklar"
/// listesi bugun icin bunlari kapsam disi tutuyor). Bugun yalnizca
/// sayfalama ve gorunurluk kurali var.
///
/// <para>
/// <paramref name="Mine"/> ile organizator kendi taslaklarini gorebilir;
/// bu bayrak olmadan taslak etkinlikler hicbir listede gorunmezdi ve
/// organizator olusturdugu kaydi bulamazdi.
/// </para>
/// </remarks>
public sealed record GetEventsQuery(bool Mine, PaginationRequest Pagination)
    : IRequest<Result<PagedResult<EventListItem>>>;

internal sealed class GetEventsQueryHandler(
    IEventQueries queries,
    ICurrentUserService currentUser)
    : IRequestHandler<GetEventsQuery, Result<PagedResult<EventListItem>>>
{
    public async Task<Result<PagedResult<EventListItem>>> Handle(
        GetEventsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Mine && currentUser.UserId is null)
            return Result.Failure<PagedResult<EventListItem>>(EventErrors.Unauthenticated);

        var admin = currentUser.IsInRole(RoleNames.Admin);

        // Gorunurluk kurali tek yerde: kendi listesini isteyen organizator
        // tum durumlari gorur, admin her seyi gorur, geri kalan herkes
        // yalnizca herkese acik durumdakileri.
        var filter = new EventListFilter(
            OrganizerId: request.Mine ? currentUser.UserId : null,
            OnlyPublic: !request.Mine && !admin,
            Pagination: request.Pagination);

        var sonuc = await queries.GetPagedAsync(filter, cancellationToken);

        return Result.Success(sonuc);
    }
}
