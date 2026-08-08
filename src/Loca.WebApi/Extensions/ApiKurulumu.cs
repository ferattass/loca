using System.Text.Json.Serialization;
using Loca.WebApi.Middleware;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Denetleyiciler, sozlesme bicimi ve hata yanitlarinin kurulumu.
/// </summary>
public static class ApiKurulumu
{
    /// <summary>Arayuzun ayri porttan gelmesine izin veren CORS politikasinin adi.</summary>
    public const string WebCors = "web";

    /// <summary>
    /// Denetleyicileri, JSON bicimini, Swagger'i ve hata yanitlarini kaydeder.
    /// </summary>
    public static IServiceCollection ApiSozlesmesiEkle(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Enum'lar sayi degil METIN olarak tasinir: yanitta "Available"
                // gorunur, 1 gorunmez. Sayi tasinsaydi istemci sihirli sabitlerle
                // karsilastirma yapmak zorunda kalir ve enum'a araya yeni bir deger
                // eklendiginde arayuz sessizce yanlis durumu gosterirdi.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Hata yanitlari RFC 7807 Problem Details formatinda doner.
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

                // Izleme kimligi hata govdesine de konuyor: kullanici destege
                // ekrandaki degeri soyleyebilsin ve o deger dogrudan log
                // satirlariyla eslessin. traceId yalnizca tek sunucu icinde
                // anlamli, bu ise istegin butun zincirini kapsiyor.
                if (ctx.HttpContext.Items.TryGetValue(
                        CorrelationIdMiddleware.BaslikAdi, out var kimlik) && kimlik is string metin)
                {
                    ctx.ProblemDetails.Extensions["correlationId"] = metin;
                }
            };
        });

        // Yakalanmamis hatalari durum koduna cevirir. UseExceptionHandler hattina takilir.
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Arayuzun ayri portta calismasina izin veren CORS politikasini kaydeder.
    /// </summary>
    /// <remarks>
    /// Arayuz ayri portta calistigi icin gerekli. Uretimde acik uclu birakilmayacak.
    /// Tek servis dagitiminda arayuz API ile ayni origin'den geldigi icin bu
    /// politika hic devreye girmiyor.
    /// </remarks>
    public static IServiceCollection TarayiciErisimiEkle(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
            options.AddPolicy(WebCors, policy => policy
                .WithOrigins(configuration["App:WebUrl"] ?? "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }
}
