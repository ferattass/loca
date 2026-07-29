using Loca.Application.Common.Interfaces;
using Loca.Infrastructure.Authentication;
using Loca.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Dogrulama uygulama acilirken yapilir, ilk istekte degil:
            // eksik yapilandirma calisma anindaki bir 500 yerine
            // aciklayici bir baslangic hatasi olarak ortaya ciksin.
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();

        // Gun 9'da Mailpit uzerinden calisan SMTP uygulamasiyla degistirilecek.
        services.AddScoped<IPasswordResetNotifier, DevelopmentPasswordResetNotifier>();

        return services;
    }
}
