namespace Loca.WebApi.Configuration;

/// <summary>
/// Barindirma saglayicisinin verdigi ortam degiskenlerini uygulamanin
/// bekledigi bicime cevirir.
/// </summary>
/// <remarks>
/// <b>Render, Railway ve Heroku ayni gelenegi kullaniyor:</b> baglanti
/// bilgisi <c>DATABASE_URL</c> ve <c>REDIS_URL</c> adiyla ve URI
/// biciminde geliyor (<c>postgres://kullanici:sifre@sunucu:5432/veritabani</c>).
/// Npgsql ve StackExchange.Redis bu bicimi ANLAMIYOR; Npgsql
/// <c>Host=...;Username=...</c>, Redis ise <c>sunucu:port,password=...</c>
/// bekliyor. Ikisi de <c>ConnectionStrings</c> bolumune yaziliyor
/// (<c>Default</c> ve <c>Redis</c>) — uygulamanin zaten okudugu yer.
///
/// <para>
/// Cevirme burada, yapilandirma okunmadan once yapiliyor. Baglanti
/// dizesini kullanan yerlerin (Persistence, Infrastructure) barindirma
/// saglayicisini tanimasi gerekmiyor: onlar hâlâ
/// <c>ConnectionStrings:Default</c> okuyor.
/// </para>
///
/// <para>
/// Degisken YOKSA hicbir sey yapilmiyor. Yerelde ve konteynerde ayarlar
/// user-secrets ve <c>.env</c>'den geliyor ve bu sinif sessizce devre
/// disi kaliyor.
/// </para>
/// </remarks>
internal static class BarindirmaAyarlari
{
    public static void Uygula(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var degerler = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } veritabani)
            degerler["ConnectionStrings:Default"] = PostgresBaglantisi(veritabani);

        if (Environment.GetEnvironmentVariable("REDIS_URL") is { Length: > 0 } redis)
            degerler["ConnectionStrings:Redis"] = RedisBaglantisi(redis);

        // Saglayici PORT veriyor ve dinlenmezse servis "hic yanit vermedi"
        // diye kapatiliyor. Kestrel'in kendi ASPNETCORE_URLS'i varsa ona
        // dokunulmuyor: acikca verilmis bir ayari ezmek, yerelde farkli bir
        // porta baglanmak isteyen kisiyi sessizce engellerdi.
        if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        }

        if (degerler.Count > 0)
            builder.Configuration.AddInMemoryCollection(degerler);
    }

    /// <summary>
    /// <c>postgres://kullanici:sifre@sunucu:5432/db</c> → Npgsql anahtar-deger.
    /// </summary>
    /// <remarks>
    /// SSL zorunlu tutuluyor: yonetilen veritabanlari uygulamayla ayni
    /// makinede degil ve baglanti internetten geciyor.
    /// <c>Trust Server Certificate</c> gerekiyor cunku bu saglayicilarin
    /// sertifikalari kendinden imzali.
    /// </remarks>
    private static string PostgresBaglantisi(string url)
    {
        var adres = new Uri(url);
        var kimlik = adres.UserInfo.Split(':', 2);

        var veritabani = adres.AbsolutePath.TrimStart('/');
        var port = adres.Port > 0 ? adres.Port : 5432;

        var kullanici = Uri.UnescapeDataString(kimlik[0]);
        var sifre = Uri.UnescapeDataString(kimlik.Length > 1 ? kimlik[1] : string.Empty);

        // Ayri ayri Invariant: birlestirilmis ifade FormattableString
        // degil string olur ve Invariant onu kabul etmez.
        var sunucu = FormattableString.Invariant(
            $"Host={adres.Host};Port={port};Database={veritabani};");

        return sunucu
            + $"Username={kullanici};Password={sifre};"
            + "SSL Mode=Require;Trust Server Certificate=true";
    }

    /// <summary>
    /// <c>redis://:sifre@sunucu:6379</c> → StackExchange.Redis bicimi.
    /// </summary>
    /// <remarks>
    /// <c>abortConnect=false</c>: Redis acilista hazir degilse uygulama
    /// yine de ayaga kalksin. Gun 6'daki karar — Redis kapaliyken kilit
    /// servisi "degraded" donuyor ve akis veritabani savunmasiyla devam
    /// ediyor.
    /// </remarks>
    private static string RedisBaglantisi(string url)
    {
        var adres = new Uri(url);
        var port = adres.Port > 0 ? adres.Port : 6379;

        var temel = FormattableString.Invariant($"{adres.Host}:{port},abortConnect=false");

        if (string.IsNullOrEmpty(adres.UserInfo))
            return temel;

        var kimlik = adres.UserInfo.Split(':', 2);
        var sifre = Uri.UnescapeDataString(kimlik.Length > 1 ? kimlik[1] : kimlik[0]);

        // rediss:// TLS demek; saglayici bunu adres semasiyla bildiriyor.
        var tls = adres.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            ? ",ssl=true"
            : string.Empty;

        return $"{temel},password={sifre}{tls}";
    }
}
