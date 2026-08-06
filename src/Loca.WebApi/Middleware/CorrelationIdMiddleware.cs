namespace Loca.WebApi.Middleware;

/// <summary>
/// Her istege bir izleme kimligi bagLar ve log satirlarina isler.
/// </summary>
/// <remarks>
/// <b>Sorun su:</b> bir kullanici "odemem gecmedi" dediginde elimizde
/// yalnizca bir zaman araligi oluyor ve o araliktaki butun log satirlari
/// birbirine karisiyor. Bir istegin uc katman boyunca biraktigi izleri tek
/// bir degere baglamak, "hangi satirlar bu istege ait" sorusunu aramaya
/// gerek birakmadan cevapliyor.
///
/// <para>
/// Kimlik istemciden GELEBILIYOR (<c>X-Correlation-Id</c>): arayuz bir
/// istek zinciri baslattiginda ayni degeri gonderirse tarayicidaki hata ile
/// sunucudaki log ayni degerle eslesiyor. Gelen deger <b>dogrulaniyor</b> —
/// uzunluk ve karakter siniri var; ham hâliyle log'a yazilsaydi istemci
/// log dosyasina satir sonu karakteri enjekte edip sahte kayit
/// uydurabilirdi (log forging).
/// </para>
///
/// <para>
/// Deger cevapta da donuyor: kullanici destege "su kimlikle hata aldim"
/// diyebilsin diye. Problem Details govdesindeki <c>traceId</c> ile ayni
/// degil — o ASP.NET'in kendi istek kimligi ve yalnizca tek sunucu
/// icinde anlamli.
/// </para>
/// </remarks>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    internal const string BaslikAdi = "X-Correlation-Id";

    // Kisa tutuluyor: log satirlarinin yarisini kaplayan bir kimlik,
    // okunmasi gereken asil bilgiyi iteler.
    private const int AzamiUzunluk = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var kimlik = GelenKimlik(context) ?? Guid.CreateVersion7().ToString("N");

        context.Items[BaslikAdi] = kimlik;

        // Baslik cevap YAZILMADAN once eklenmeli; govde akmaya
        // basladiktan sonra basliklara dokunmak istisna firlatiyor.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[BaslikAdi] = kimlik;
            return Task.CompletedTask;
        });

        // Kapsam icinde uretilen HER log satiri bu kimligi tasiyor;
        // handler'larin ayrica loglamasi gerekmiyor.
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = kimlik }))
        {
            await next(context);
        }
    }

    /// <returns>Kabul edilebilir bir kimlik ya da <c>null</c>.</returns>
    /// <remarks>
    /// Gecersiz bir deger reddedilmiyor, YOK SAYILIYOR: istemcinin
    /// gonderdigi izleme kimliginin bicimi yuzunden gercek bir istegi
    /// reddetmek, cozdugunden fazla sorun cikarirdi.
    /// </remarks>
    private static string? GelenKimlik(HttpContext context)
    {
        var gelen = context.Request.Headers[BaslikAdi].ToString();

        if (string.IsNullOrWhiteSpace(gelen) || gelen.Length > AzamiUzunluk)
            return null;

        foreach (var karakter in gelen)
        {
            // Yalnizca harf, rakam, tire ve alt cizgi. Satir sonu ve
            // kontrol karakterleri bu suzgecten gecemiyor.
            var uygun = char.IsAsciiLetterOrDigit(karakter) || karakter is '-' or '_';

            if (!uygun)
                return null;
        }

        return gelen;
    }
}
