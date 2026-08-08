using System.Globalization;
using System.Threading.RateLimiting;
using Loca.Application.Common.Authentication;
using Loca.WebApi.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Hiz sinirlamasinin kurulumu.
/// </summary>
public static class HizSinirlamaKurulumu
{
    /// <summary>
    /// Kimlik uclari, sifre sifirlama ve genel tavan icin hiz sinirlarini kaydeder.
    /// </summary>
    /// <remarks>
    /// Asil hedef kimlik uclari: sifre denemesi, kayit ve sifre sifirlama
    /// istegi. Bu uclar olmadan bir sozluk saldirisi saniyede yuzlerce sifre
    /// deneyebiliyor ve BCrypt'in yavasligi bu durumda savunma degil, sunucuyu
    /// tuketen bir maliyet hâline geliyor.
    ///
    /// <para>
    /// Bolutleme IP'ye gore: kimlik dogrulanmamis uclarda kullanici kimligi
    /// yok. IP paylasiliyor olabilir (kurumsal ag, mobil operator), bu yuzden
    /// esikler tek bir insanin yapacagindan cok daha yukseklerde.
    /// Degerler yapilandirmadan: uretim degerleri uctan uca betikleri kiriyor
    /// (yaris testi tek IP'den 50 kullanici kaydediyor). Gelistirmede genis,
    /// guvenlik betigi ise API'yi dar degerlerle kaldirip siniri gercekten
    /// asiyor — sinirlama boylece hem denenmis oluyor hem de digerlerini
    /// engellemiyor.
    /// </para>
    /// </remarks>
    public static IServiceCollection HizSinirlamaEkle(
        this IServiceCollection services, IConfiguration configuration)
    {
        var hizSinirlari = configuration
            .GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(secenekler =>
        {
            secenekler.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Istemci ne zaman tekrar deneyecegini bilmeli; bu baslik olmadan
            // dogru davranan bir istemci bile korlemesine yeniden dener.
            secenekler.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var sure))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)sure.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    """{"title":"Cok fazla istek","status":429,"detail":"Kisa surede cok fazla deneme yapildi. Biraz bekleyip tekrar deneyin.","code":"TooManyRequests"}""",
                    cancellationToken);
            };

            secenekler.AddPolicy(RateLimitPolicies.Auth, context =>
                SabitPencere(IstemciAnahtari(context), hizSinirlari.Auth));

            // Sifre sifirlama daha dar: her istek bir e-posta gonderiyor ve bu
            // uc, baskasinin kutusuna posta yagdirmak icin kullanilabilir.
            secenekler.AddPolicy(RateLimitPolicies.PasswordReset, context =>
                SabitPencere(IstemciAnahtari(context), hizSinirlari.PasswordReset));

            // Genel sinir yalnizca kaba bir tavan: normal kullanimda goze
            // carpmayacak kadar yuksek, tek bir istemcinin sunucuyu tuketmesini
            // engelleyecek kadar dusuk.
            //
            // Saglik uclari DISARIDA: yonlendirici saniyede birkac kez soruyor ve
            // sinira takilsaydi saglikli bir sunucu olu sayilirdi.
            secenekler.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("saglik")
                    : SabitPencere(IstemciAnahtari(context), hizSinirlari.Global));
        });

        return services;
    }

    private static RateLimitPartition<string> SabitPencere(string anahtar, RateLimitKurali kural) =>
        RateLimitPartition.GetFixedWindowLimiter(
            anahtar,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = kural.PermitLimit,
                Window = kural.Window,
                // Kuyruk YOK: bekletilen bir giris istegi, saldirgan icin
                // ucretsiz bir yavaslatma araci olurdu.
                QueueLimit = 0
            });

    // Kimlik dogrulanmissa kullanici, degilse IP. Oturum acmis bir kullanici
    // IP'sini degistirerek siniri sifirlayamasin diye kimlik once geliyor.
    private static string IstemciAnahtari(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? $"u:{context.User.FindFirst(ClaimNames.Subject)?.Value ?? context.User.Identity.Name}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor"}";
}
