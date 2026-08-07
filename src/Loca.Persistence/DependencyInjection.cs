using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Venues.GetHallAvailability;
using Loca.Domain.Repositories;
using Loca.Persistence.Interceptors;
using Loca.Persistence.Queries;
using Loca.Persistence.Repositories;
using Loca.Persistence.Seeding;
using Loca.Persistence.Settings;
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
        services.AddScoped<IReservationQueries, ReservationQueries>();
        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<IAdminQueries, AdminQueries>();

        // Calisma aninda degistirilebilen ayarlar. Sir olanlar sifreli
        // saklaniyor; sifreleyici Infrastructure'da.
        services.AddScoped<ISettingsStore, SettingsStore>();

        return services;
    }
}
