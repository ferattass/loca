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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<City> Cities => Set<City>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<SeatLayout> SeatLayouts => Set<SeatLayout>();
    public DbSet<SeatSection> SeatSections => Set<SeatSection>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    public DbSet<OrganizerProfile> OrganizerProfiles => Set<OrganizerProfile>();
    public DbSet<OrganizerApplication> OrganizerApplications => Set<OrganizerApplication>();
    public DbSet<StudentVerification> StudentVerifications => Set<StudentVerification>();

    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<EventSeat> EventSeats => Set<EventSeat>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
    ///
    /// <para>
    /// <b>Var olan filtre EZILMEZ, birlestirilir.</b> Onceki hâl
    /// <c>SetQueryFilter</c>'i dogrudan cagirdigi icin konfigurasyon
    /// sinifinda yazilmis bir filtreyi sessizce siliyordu — bu metot
    /// <c>ApplyConfigurationsFromAssembly</c>'den SONRA calisiyor.
    /// Silinen filtre hata vermez, yalnizca beklenen kaydi disarida
    /// birakmayi bırakır: en tehlikeli hata turu.
    /// </para>
    /// </remarks>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var mevcut = entityType.GetQueryFilter();

            // Mevcut filtrenin parametresi yeniden kullaniliyor; iki ayri
            // parametreyle kurulan ifadeler birlestirilemez, once yeniden
            // baglanmasi gerekirdi.
            var parameter = mevcut?.Parameters[0]
                ?? Expression.Parameter(entityType.ClrType, "e");

            // e => !e.IsDeleted ifadesi tip bilinmeden kuruldugu icin elle olusturuluyor.
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));

            Expression body = Expression.Not(isDeleted);

            if (mevcut is not null)
                body = Expression.AndAlso(mevcut.Body, body);

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
