using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Auth.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Auth.Me;

/// <summary>
/// Token'in sahibi olan kullaniciyi doner.
/// </summary>
/// <remarks>
/// Bilgi token claim'lerinden de okunabilirdi ama veritabanindan okunuyor:
/// token uretildikten sonra rol degismis ya da hesap pasiflestirilmis olabilir.
/// Token 15 dakika yasar, bu surede degisiklik olabilir.
/// </remarks>
public sealed record GetCurrentUserQuery : IRequest<Result<UserSummary>>;

internal sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository users)
    : IRequestHandler<GetCurrentUserQuery, Result<UserSummary>>
{
    public async Task<Result<UserSummary>> Handle(
        GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return Result.Failure<UserSummary>(
                Error.Unauthorized("Auth.NotAuthenticated", "Oturum bulunamadi."));

        var user = await users.GetByIdAsync(userId, cancellationToken);

        // Token gecerli ama kullanici silinmis olabilir.
        if (user is null)
            return Result.Failure<UserSummary>(
                Error.NotFound("User.NotFound", "Kullanici bulunamadi."));

        var roles = user.UserRoles
            .Where(userRole => userRole.Role is not null)
            .Select(userRole => userRole.Role!.Name)
            .ToList();

        return Result.Success(new UserSummary(user.Id, user.Email, user.FullName, roles));
    }
}
