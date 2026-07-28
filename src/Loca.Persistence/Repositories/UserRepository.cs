using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class UserRepository(LocaDbContext context) : IUserRepository
{
    /// <remarks>
    /// Roller de birlikte yuklenir: giristen hemen sonra token'a rol claim'leri
    /// yazilacak. Ayri sorgu atilsaydi her girise iki gidis donus olurdu.
    /// </remarks>
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(email);

        return context.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Email == normalized, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(email);

        return context.Users.AnyAsync(user => user.Email == normalized, cancellationToken);
    }

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default) =>
        context.Roles.FirstOrDefaultAsync(role => role.Name == roleName, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
