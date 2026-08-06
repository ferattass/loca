using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Admin.Settings;
using Loca.Infrastructure.Email;
using Loca.Infrastructure.Authentication;
using Loca.Infrastructure.Concurrency;
using Loca.Infrastructure.Messaging;
using Loca.Infrastructure.Payments;
using Loca.Infrastructure.Services;
using Loca.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
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

        // --- E-posta --------------------------------------------------------
        // Sir ayarlar (SMTP sifresi) Data Protection ile sifreleniyor;
        // veritabani yedegini eline geciren biri cozemesin.
        var dataProtection = services.AddDataProtection();

        // Anahtarlarin KALICI olmasi sart. Varsayilan konum konteynerde
        // imajin kendi dosya sisteminde kaliyor, yani her deploy'da yeni
        // anahtar uretiliyor ve o ana kadar sifrelenmis her sey cozulemez
        // hâle geliyor — yonetici SMTP sifresini her seferinde yeniden
        // girmek zorunda kalirdi. Uygulama cokmuyor (cozulemeyen sir
        // "tanimli degil" sayiliyor), bu yuzden hata sessiz olurdu.
        var anahtarYolu = configuration["DataProtection:KeyPath"];

        if (!string.IsNullOrWhiteSpace(anahtarYolu))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(anahtarYolu));
        }

        services.AddMemoryCache();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

        services.AddScoped<SmtpOptionsProvider>();
        services.AddScoped<ISmtpSettingsReader>(saglayici =>
            saglayici.GetRequiredService<SmtpOptionsProvider>());
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Sifirlama baglantisi artik gercekten e-postayla gidiyor.
        // Gelistirme uygulamasi (token'i log'a yazan) YEDEK OLARAK
        // BIRAKILMADI: sessizce ona dusulseydi, uretimde SMTP'yi kurmayi
        // unutan biri hicbir uyari almadan token'lari log dosyasina
        // yazdirmis olurdu.
        services.AddScoped<IPasswordResetNotifier, EmailPasswordResetNotifier>();

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

        // Gun 9'da SMTP uygulamasiyla degistirilecek; outbox isi degismeyecek.
        services.AddScoped<IOutboxDispatcher, LoggingOutboxDispatcher>();

        // Saglayici SECIMI acilista yapiliyor, her istekte degil: hangi
        // saglayicinin kullanildigi uygulama omru boyunca degismez ve
        // Iyzico secilmediginde onun yapilandirmasi hic aranmaz.
        var secilenSaglayici =
            configuration[$"{PaymentOptions.SectionName}:Provider"] ?? "Mock";

        // Ayar saglayicisi HER ZAMAN kayitli: yonetim paneli anahtarlari
        // okuyup yaziyor ve bu, hangi saglayicinin secildiginden bagimsiz.
        // Yalnizca Iyzico secildiginde kaydedilseydi panel Mock ortaminda
        // "ayarlar okunamadi" derdi.
        services.AddScoped<IyzicoSettingsProvider>();
        services.AddScoped<IPaymentSettingsReader>(saglayici =>
            saglayici.GetRequiredService<IyzicoSettingsProvider>());

        if (secilenSaglayici.Equals("Iyzico", StringComparison.OrdinalIgnoreCase))
        {
            // ValidateOnStart KALDIRILDI: anahtarlar artik veritabaninda da
            // olabiliyor, acilis aninda eksik gorunmeleri normal ve
            // uygulamanin bu yuzden hic ayaga kalkmamasi yanlis olurdu.
            // Eksiklik odeme baslatilirken anlasiliyor.

            // Typed client: HttpClient omrunu fabrika yonetiyor. Elle
            // olusturulan bir HttpClient ya soket tuketir ya da DNS
            // degisikligini gormez.
            services.AddHttpClient<IyzicoPaymentProvider>();

            services.AddScoped<IPaymentService>(serviceProvider =>
                serviceProvider.GetRequiredService<IyzicoPaymentProvider>());
        }
        else
        {
            // Basarisiz saglayici yalnizca "odeme basarisiz olursa koltuklar
            // serbest kaliyor mu" senaryosunu calistirmak icin var.
            services.AddScoped<IPaymentService>(serviceProvider =>
            {
                var ayarlar = serviceProvider.GetRequiredService<IOptions<PaymentOptions>>().Value;

                return ayarlar.Provider.Equals("FailedMock", StringComparison.OrdinalIgnoreCase)
                    ? new FailedPaymentProvider(
                        serviceProvider.GetRequiredService<ILogger<FailedPaymentProvider>>())
                    : new MockPaymentProvider(
                        ayarlar, serviceProvider.GetRequiredService<ILogger<MockPaymentProvider>>());
            });
        }

        return services;
    }
}
