using Loca.Application.Common.Interfaces;
using Loca.Infrastructure.Authentication;
using Loca.Infrastructure.Concurrency;
using Loca.Infrastructure.Payments;
using Loca.Infrastructure.Services;
using Loca.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // --- Eszamanlilik (Gun 6) ------------------------------------------

        services
            .AddOptions<ReservationOptions>()
            .Bind(configuration.GetSection(ReservationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Kilit suresi ve bilet limiti uygulama omru boyunca degismez;
        // her istekte yeniden hesaplanmasinin karsiligi yok.
        services.AddSingleton<IReservationPolicy>(serviceProvider =>
            new ReservationPolicy(
                serviceProvider.GetRequiredService<IOptions<ReservationOptions>>().Value));

        // Baglanti tembel kuruluyor: Redis kapaliyken uygulama ayaga kalkmali.
        // Varsayilan deger yerel gelistirme icin; konteynerde
        // ConnectionStrings__Redis ortam degiskeninden gelir.
        services.AddSingleton(serviceProvider => new RedisConnection(
            configuration.GetConnectionString("Redis") ?? "localhost:6379",
            serviceProvider.GetRequiredService<ILogger<RedisConnection>>()));

        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // --- Odeme (Gun 7) --------------------------------------------------

        services
            .AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITicketCodeGenerator, TicketCodeGenerator>();

        // Saglayici yapilandirmadan seciliyor. Basarisiz saglayici yalnizca
        // "odeme basarisiz olursa koltuklar serbest kaliyor mu" senaryosunu
        // calistirmak icin var; uretimde gercek saglayici gelecek.
        services.AddScoped<IPaymentService>(serviceProvider =>
        {
            var ayarlar = serviceProvider.GetRequiredService<IOptions<PaymentOptions>>().Value;

            return ayarlar.Provider.Equals("FailedMock", StringComparison.OrdinalIgnoreCase)
                ? new FailedPaymentProvider(
                    serviceProvider.GetRequiredService<ILogger<FailedPaymentProvider>>())
                : new MockPaymentProvider(
                    ayarlar, serviceProvider.GetRequiredService<ILogger<MockPaymentProvider>>());
        });

        return services;
    }
}
