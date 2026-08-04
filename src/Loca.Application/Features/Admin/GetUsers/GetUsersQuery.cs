using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using MediatR;

namespace Loca.Application.Features.Admin.GetUsers;

public sealed record GetUsersQuery(AdminKullaniciFiltresi Filtre)
    : IRequest<Result<PagedResult<AdminKullanici>>>;

internal sealed class GetUsersQueryHandler(IAdminQueries queries)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<AdminKullanici>>>
{
    public async Task<Result<PagedResult<AdminKullanici>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sayfa = await queries.GetUsersAsync(request.Filtre, cancellationToken);

        return Result.Success(sayfa);
    }
}
