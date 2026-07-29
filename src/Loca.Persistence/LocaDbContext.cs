using System.Linq.Expressions;
using Loca.Domain.Common;
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

    public DbSet<City> Cities => Set<City>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<SeatLayout> SeatLayouts => Set<SeatLayout>();
    public DbSet<SeatSection> SeatSections => Set<SeatSection>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Konfigurasyonlar tek tek eklenmez; yeni bir entity yapilandirmasi
        // yazildiginda kendiliginden bulunur.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocaDbContext).Assembly);

        ApplySoftDeleteFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// <see cref="ISoftDeletable"/> uygulayan her varliga <c>e =&gt; !e.IsDeleted</c>
    /// filtresini takar.
    /// </summary>
    /// <remarks>
    /// Filtre her konfigurasyon sinifinda elle yazilabilirdi ama bir gun biri
    /// unutulur ve silinmis kayitlar listelerde gorunmeye baslar — bu tur bir
    /// hata sessizdir, hata vermez. Burada tip taranarak otomatik uygulaniyor;
    /// yeni bir silinebilir varlik eklendiginde ek is gerekmiyor.
    ///
    /// <para>
    /// Filtreyi atlamak gerektiginde (ornegin admin denetim ekrani)
    /// <c>IgnoreQueryFilters()</c> kullanilir.
    /// </para>
    /// </remarks>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            // e => !e.IsDeleted ifadesi tip bilinmeden kuruldugu icin elle olusturuluyor.
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);

            entityType.SetQueryFilter(filter);
        }
    }
}
