using Loca.Domain.Repositories;
using Loca.Persistence.Interceptors;
using Loca.Persistence.Repositories;
using Loca.Persistence.Seeding;
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

        return services;
    }
}
