using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence;

/// <summary>
/// Uygulamanin veritabani baglami.
/// </summary>
/// <remarks>
/// <see cref="IUnitOfWork"/> arayuzunu de uygular; handler'lar bu sinifi degil
/// arayuzu gorur. Boylece uygulama katmani EF Core'u tanimaz ve handler testleri
/// veritabani olmadan yazilabilir.
/// </remarks>
public sealed class LocaDbContext(DbContextOptions<LocaDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Konfigurasyonlar tek tek eklenmez; yeni bir entity yapilandirmasi
        // yazildiginda kendiliginden bulunur.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocaDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
