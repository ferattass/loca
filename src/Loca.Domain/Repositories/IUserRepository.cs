using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Kullanici erisimi. Arayuz Domain'de, uygulamasi Persistence'ta durur —
/// bagimlilik oku boylece disaridan iceriye akar.
/// </summary>
public interface IUserRepository
{
    /// <param name="email">Normalize edilmemis hâli verilebilir; uygulama kucuk harfe cevirir.</param>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);

    void Add(User user);
}
