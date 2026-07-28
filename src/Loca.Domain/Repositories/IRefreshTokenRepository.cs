using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// Token'i sahibi ve rolleriyle birlikte getirir.
    /// </summary>
    /// <remarks>
    /// Iptal edilmis token da doner — cunku yeniden kullanim tespiti tam olarak
    /// "iptal edilmis bir token geldi mi" sorusunu sorar. Filtrelenirse o saldiri
    /// sinyali gorunmez olur.
    /// </remarks>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Bir kullanicinin iptal edilmemis tum token'lari.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}
