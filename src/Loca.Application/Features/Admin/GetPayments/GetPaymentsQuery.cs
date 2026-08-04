using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using MediatR;

namespace Loca.Application.Features.Admin.GetPayments;

/// <summary>Yonetim panelindeki odeme listesi.</summary>
/// <remarks>
/// Kullaniciya ozel <c>GET /payments/{id}</c> ucundan farkli: burada
/// sahiplik kisiti YOK, admin tum odemeleri gorur. Bu yuzden uc de ayri
/// (<c>/admin/payments</c>) ve <c>AdminOnly</c> policy'siyle korunuyor —
/// ayni ucun bir parametreyle "hepsini getir" moduna gecmesi, o
/// parametrenin bir gun yanlislikla acilmasi riski tasirdi.
/// </remarks>
public sealed record GetPaymentsQuery(AdminOdemeFiltresi Filtre)
    : IRequest<Result<PagedResult<AdminOdemeSatiri>>>;

internal sealed class GetPaymentsQueryHandler(IAdminQueries queries)
    : IRequestHandler<GetPaymentsQuery, Result<PagedResult<AdminOdemeSatiri>>>
{
    public async Task<Result<PagedResult<AdminOdemeSatiri>>> Handle(
        GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sayfa = await queries.GetPaymentsAsync(request.Filtre, cancellationToken);

        return Result.Success(sayfa);
    }
}
