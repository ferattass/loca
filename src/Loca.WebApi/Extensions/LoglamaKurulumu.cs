using Serilog;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Yapilandirilmis loglamanin kurulumu.
/// </summary>
public static class LoglamaKurulumu
{
    /// <summary>
    /// Serilog'u yapilandirmadan okuyup host'a baglar.
    /// </summary>
    /// <remarks>
    /// Serilog paketi bastan beri referansliydi ama hic baglanmamisti; loglar
    /// varsayilan konsol saglayicisindan cikiyordu. Yapilandirilmis loglamanin
    /// farki, satirin metin degil ALAN tasimasi: "OdemeId: 019f..." bir metin
    /// parcasi degil sorgulanabilir bir alan oluyor ve bir odemenin butun izi
    /// tek bir filtreyle toplanabiliyor.
    /// </remarks>
    public static WebApplicationBuilder SerilogKur(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, yapilandirma) => yapilandirma
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            // Makine adi cok ornekli calisirken "hangi sunucuda" sorusunu
            // cevapliyor; tek sunucuda zararsiz bir alan.
            .Enrich.WithProperty("Uygulama", "Loca.WebApi"));

        return builder;
    }
}
