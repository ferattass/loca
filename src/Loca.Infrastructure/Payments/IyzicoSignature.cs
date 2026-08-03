using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Iyzico IYZWSv2 yetkilendirme baslarigi icin imzalama yardimcisi.
/// </summary>
/// <remarks>
/// Resmi <c>Iyzipay</c> NuGet paketi kullanilmiyor (bkz. <see cref="IyzicoPaymentProvider"/>
/// uzerindeki aciklama); bu yuzden IYZWSv2 imzalama burada elle uygulaniyor.
/// Algoritma: <c>rastgele deger + istek yolu + istek govdesi</c> HMAC-SHA256
/// ile SecretKey kullanilarak imzalanir, sonucu hex'e cevrilir ve
/// <c>apiKey=...&amp;randomKey=...&amp;signature=...</c> parametre dizesi
/// Base64 ile kodlanip <c>IYZWSv2 &lt;base64&gt;</c> seklinde donulur.
/// </remarks>
internal sealed class IyzicoSignature
{
    // Yalnizca static uyeler tasindigindan disaridan ornek olusturulmasinin
    // anlami yok; yine de "internal sealed class" kurali geregi static class
    // yerine bu sekilde tanimlandi.
    private IyzicoSignature()
    {
    }

    /// <summary>
    /// Her istekte benzersiz olmasi gereken rastgele deger.
    /// </summary>
    /// <remarks>
    /// Ayni deger iki farkli istekte tekrar etseydi, o istek icin uretilmis
    /// imza baska bir istekte de gecerli sayilabilir ve bir cagriyi
    /// dinleyip aynen tekrar gondermek (replay) mumkun hale gelirdi. Zaman
    /// damgasi ile rastgele sayinin birlikte kullanilmasi bu riski ortadan
    /// kaldirir.
    /// </remarks>
    public static string RastgeleDeger() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100000, 999999)}");

    /// <summary>
    /// IYZWSv2 yetkilendirme basligini (<c>Authorization</c> header degeri) uretir.
    /// </summary>
    /// <param name="apiKey">Istekte acikca tasinan anahtar; sir degildir.</param>
    /// <param name="secretKey">
    /// Yalnizca HMAC anahtari olarak kullanilir. HMAC tek yonlu oldugundan
    /// uretilen imzadan SecretKey'e geri donus mumkun degildir; bu yuzden
    /// imza donen deger icinde tasinsa bile sir sizmis olmaz.
    /// </param>
    /// <param name="randomKey"><see cref="RastgeleDeger"/> ile uretilen ve
    /// <c>x-iyzi-rnd</c> basligiyla ayni gonderilmesi gereken deger.</param>
    /// <param name="uriPath">Sorgu dizesi ve adres/sunucu adi olmadan istek
    /// yolu, orn. <c>/payment/iyzipos/checkoutform/initialize/auth/ecom</c>.</param>
    /// <param name="requestBody">Istegin JSON govdesi.</param>
    public static string YetkilendirmeBasligi(
        string apiKey, string secretKey, string randomKey, string uriPath, string requestBody)
    {
        var imzalanacakVeri = randomKey + uriPath + requestBody;

        var imzaBaytlari = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(imzalanacakVeri));

        var imza = Convert.ToHexString(imzaBaytlari).ToLowerInvariant();

        var yetkilendirmeParametreleri = string.Create(
            CultureInfo.InvariantCulture, $"apiKey={apiKey}&randomKey={randomKey}&signature={imza}");

        var base64ParametreDizesi = Convert.ToBase64String(Encoding.UTF8.GetBytes(yetkilendirmeParametreleri));

        return $"IYZWSv2 {base64ParametreDizesi}";
    }
}
