using System.Linq.Expressions;
using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    /// <summary>
    /// PostgreSQL'in benzersizlik ihlali hata kodu.
    /// </summary>
    private const string UniqueViolationSqlState = "23505";

    /// <summary>
    /// Kaydeder ve ORM'e ozgu istisnalari katman-notr tiplere cevirir.
    /// </summary>
    /// <remarks>
    /// Ceviri burada yapiliyor cunku uygulama katmani EF Core'u tanimiyor;
    /// <c>DbUpdateConcurrencyException</c>'i handler icinde yakalamak,
    /// veritabani teknolojisini is kurallarinin arasina sokmak olurdu.
    ///
    /// <para>
    /// Kisit ADI da tasiniyor: rezervasyon akisinda idempotency cakismasi
    /// ile koltuk tekilligi cakismasi ayni istisna tipiyle gelir ama biri
    /// "mevcut kaydi don", digeri "409 don" demek. Ayirt edilemeselerdi
    /// tekrar gonderilen bir istek hata olarak donerdi.
    /// </para>
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "Kayit islem sirasinda baskasi tarafindan degistirildi.", exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: UniqueViolationSqlState } postgres)
        {
            throw new UniqueConstraintViolationException(
                "Benzersizlik kurali ihlal edildi.", postgres.ConstraintName, exception);
        }
    }

    public async Task<ITransactionScope> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);

        return new EfTransactionScope(transaction);
    }

    /// <summary>
    /// EF transaction'ini katman-notr arayuzun arkasina alir.
    /// </summary>
    /// <remarks>
    /// Cift <c>DisposeAsync</c> guvenli: rezervasyon akisinda idempotency
    /// yarisi yakalandiginda transaction erken geri aliniyor, ardindan
    /// <c>await using</c> ayni nesneyi bir kez daha birakiyor. Bayrak
    /// olmasaydi bu, davranisi EF'in ic detayina bagli bir cagri olurdu.
    /// </remarks>
    private sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
    {
        private bool _disposed;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Commit edilmemis transaction burada geri alinir.
            await transaction.DisposeAsync();
        }
    }

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
