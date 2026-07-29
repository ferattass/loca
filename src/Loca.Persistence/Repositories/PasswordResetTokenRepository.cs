using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class PasswordResetTokenRepository(LocaDbContext context)
    : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default) =>
        context.PasswordResetTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PasswordResetToken>> GetUsableByUserIdAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        await context.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.UsedAt == null &&
                token.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);

    public void Add(PasswordResetToken token) => context.PasswordResetTokens.Add(token);
}
