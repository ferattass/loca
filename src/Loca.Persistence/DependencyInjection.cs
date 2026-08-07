using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Events.Documents;
using Loca.Application.Features.Venues.GetHallAvailability;
using Loca.Domain.Repositories;
using Loca.Persistence.Interceptors;
using Loca.Persistence.Queries;
using Loca.Persistence.Repositories;
using Loca.Persistence.Seeding;
using Loca.Persistence.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loca.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default tanimli degil. Gelistirmede user-secrets, " +
                "konteynerde ConnectionStrings__Default ortam degiskeni kullanilir.");

        services
            .AddOptions<AdminSeedOptions>()
            .Bind(configuration.GetSection(AdminSeedOptions.SectionName));

        // Data Protection anahtarlari: yol verilmemisse VERITABANINA.
        //
        // Kayit burada cunku DbContext burada; Infrastructure'da yapilsaydi
        // altyapi veri katmanina bagimli olurdu. AddDataProtection ikinci
        // kez cagrilmasi sorun degil, yapilandirma birikimli.
        //
        // Ucretsiz barindirma katmanlarinda dosya sistemi gecici ve kalici
        // disk yok: her uyanista yeni anahtar uretilir ve panelden girilmis
        // iyzico anahtarlariyla SMTP sifresi cozulemez hale gelirdi.
        // Cozulemeyen sir "tanimli degil" sayildigi icin uygulama cokmez —
        // hata sessizdir.
        if (string.IsNullOrWhiteSpace(configuration["DataProtection:KeyPath"]))
        {
            services.AddDataProtection().PersistKeysToDbContext<LocaDbContext>();
        }

        services.AddScoped<DatabaseSeeder>();

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<LocaDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        // Ayni DbContext ornegi hem UnitOfWork hem de repository'ler tarafindan
        // kullanilir; aksi hâlde repository'ler farkli degisiklik takipcilerine
        // yazar ve SaveChanges bazi degisiklikleri gormezdi.
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<LocaDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IHallRepository, HallRepository>();
        services.AddScoped<ISeatLayoutRepository, SeatLayoutRepository>();
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        services.AddScoped<IEventDocumentRepository, EventDocumentRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IOrganizerRepository, OrganizerRepository>();
        services.AddScoped<IStudentVerificationRepository, StudentVerificationRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // Okuma tarafi: arayuzu Application'da, projeksiyonu burada.
        services.AddScoped<IEventQueries, EventQueries>();
        services.AddScoped<IHallAvailabilityQueries, HallAvailabilityQueries>();
        services.AddScoped<IEventDocumentQueries, EventDocumentQueries>();
        services.AddScoped<IReservationQueries, ReservationQueries>();
        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<IAdminQueries, AdminQueries>();

        // Calisma aninda degistirilebilen ayarlar. Sir olanlar sifreli
        // saklaniyor; sifreleyici Infrastructure'da.
        services.AddScoped<ISettingsStore, SettingsStore>();

        return services;
    }
}
