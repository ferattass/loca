using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using MediatR;

namespace Loca.Application.Features.Events.GetEventCategories;

/// <remarks>
/// Etkinlik olusturma formunun ilk alani. Anonim erisime acik: kategori
/// listesi ana sayfadaki filtre serididir de.
/// </remarks>
public sealed record GetEventCategoriesQuery : IRequest<Result<IReadOnlyList<EventCategoryItem>>>;

internal sealed class GetEventCategoriesQueryHandler(IEventQueries queries)
    : IRequestHandler<GetEventCategoriesQuery, Result<IReadOnlyList<EventCategoryItem>>>
{
    public async Task<Result<IReadOnlyList<EventCategoryItem>>> Handle(
        GetEventCategoriesQuery request, CancellationToken cancellationToken)
    {
        var kategoriler = await queries.GetCategoriesAsync(cancellationToken);

        return Result.Success(kategoriler);
    }
}
