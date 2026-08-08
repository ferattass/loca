using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Ters vekil arkasinda calisirken istemcinin gercek adresinin okunmasi.
/// </summary>
public static class VekilKurulumu
{
    /// <summary>
    /// Guvenilen vekillerden gelen X-Forwarded-* basliklarinin okunmasini saglar.
    /// </summary>
    /// <remarks>
    /// Uretimde uygulama bir ters vekilin (nginx, yuk dengeleyici, PaaS yonlendirici)
    /// arkasinda duruyor. O durumda RemoteIpAddress vekilin adresi oluyor ve
    /// hiz sinirlamasi butun kullanicilari tek bir istemci sayardi; odeme
    /// saglayicisina giden alici IP'si de yanlis olurdu.
    ///
    /// <para>
    /// <b>Guvenilen vekil listesi zorunlu.</b> Liste bos birakilip basliklar
    /// kayitsizca okunsaydi, X-Forwarded-For'u ISTEMCI de gonderebildigi icin
    /// herkes kendi IP'sini uydurabilir ve hiz sinirlamasini her istekte farkli
    /// bir adres yazarak tamamen atlatabilirdi.
    /// </para>
    /// </remarks>
    public static IServiceCollection VekilBasliklariEkle(
        this IServiceCollection services, IConfiguration configuration)
    {
        var guvenilenVekiller = configuration
            .GetSection("App:TrustedProxies").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(secenekler =>
        {
            secenekler.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Varsayilan olarak localhost guvenilir sayiliyor; onu da kaldirip
            // yalnizca acikca yazilan adresleri kabul ediyoruz.
            secenekler.KnownNetworks.Clear();
            secenekler.KnownProxies.Clear();

            foreach (var vekil in guvenilenVekiller)
            {
                if (IPAddress.TryParse(vekil, out var adres))
                    secenekler.KnownProxies.Add(adres);
            }
        });

        return services;
    }
}
