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
/// <paramref name="Mine"/> ile organizator kendi taslaklarini gorebilir;
/// bu bayrak olmadan taslak etkinlikler hicbir listede gorunmezdi ve
/// organizator olusturdugu kaydi bulamazdi.
///
/// <para>
/// <b>Suzme sunucuda.</b> Kesfet ekrani zaten bir sayfa etkinlik cekiyor
/// ve kategorileri o listeden turetebilirdi; ama o zaman filtre yalnizca
/// cekilmis sayfa uzerinde calisir, katalogun geri kalani disarida
/// kalirdi — kullanici "bu kategoride uc etkinlik var" sanardi.
/// </para>
/// </remarks>
public sealed record GetEventsQuery(
    bool Mine,
    PaginationRequest Pagination,
    Guid? CategoryId = null,
    string? Search = null)
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
            Pagination: request.Pagination,
            CategoryId: request.CategoryId,
            // Bos arama "bos metni ara" demek degil "arama yok" demek.
            Search: string.IsNullOrWhiteSpace(request.Search) ? null : request.Search);

        var sonuc = await queries.GetPagedAsync(filter, cancellationToken);

        return Result.Success(sonuc);
    }
}
