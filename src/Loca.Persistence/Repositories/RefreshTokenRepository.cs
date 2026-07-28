using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class RefreshTokenRepository(LocaDbContext context) : IRefreshTokenRepository
{
    /// <remarks>
    /// Iptal edilmis token'lar da doner. Filtrelenseydi "iptal edilmis token
    /// tekrar geldi" sinyali kaybolur ve yeniden kullanim tespiti calismazdi.
    /// </remarks>
    public Task<RefreshToken?> GetByTokenAsync(
        string token, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Include(refreshToken => refreshToken.User)
                .ThenInclude(user => user!.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(refreshToken => refreshToken.Token == token, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(refreshToken =>
                refreshToken.UserId == userId && refreshToken.RevokedAt == null)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);
}
