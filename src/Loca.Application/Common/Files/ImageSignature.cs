namespace Loca.Application.Common.Files;

/// <summary>
/// Yuklenen dosyanin gercekten izin verilen bir gorsel olup olmadigini
/// icerigin ilk baytlarina bakarak dogrular.
/// </summary>
/// <remarks>
/// <b>Uzantiya ve MIME basligina guvenilmez.</b> Ikisini de istemci
/// belirliyor: <c>zararli.exe</c> dosyasi <c>resim.png</c> adiyla ve
/// <c>image/png</c> basligiyla gonderilebilir. Dosyanin turunu soyleyen tek
/// guvenilir kaynak icerigin kendisi — her formatin basinda sabit bir imza
/// (magic byte) durur.
///
/// <para>
/// Saf mantik oldugu icin Application katmaninda ve diske ya da HTTP'ye
/// dokunmadan test edilebiliyor.
/// </para>
/// </remarks>
public static class ImageSignature
{
    /// <summary>Gorsel yuklemede (afis, mekan kapagi) izin verilen uzantilar.</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>
    /// Belge yuklemede (sahne kira sozlesmesi, izin yazisi) izin verilenler.
    /// </summary>
    /// <remarks>
    /// <b>Gorsel listesinden AYRI.</b> Afis alanina PDF yuklenebilseydi
    /// vitrinde cizilemeyen bir "gorsel" olurdu; belge alani ise pratikte
    /// PDF olmadan ise yaramaz — sozlesmeler taranmis PDF geliyor. Iki
    /// listenin ayri olmasi, hangi alanin neyi kabul ettigini tek yerden
    /// okunur kiliyor.
    /// </remarks>
    public static readonly IReadOnlyList<string> AllowedDocumentExtensions =
        [".pdf", ".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Imza karsilastirmasi icin okunmasi yeterli bayt sayisi.</summary>
    public const int RequiredHeaderLength = 12;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] Webp = [0x57, 0x45, 0x42, 0x50]; // "WEBP"
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46]; // "%PDF"

    /// <summary>
    /// Icerigin basina bakarak taninan turu doner; taninmiyorsa <c>null</c>.
    /// </summary>
    /// <returns>Nokta ile baslayan uzanti: <c>.png</c> gibi.</returns>
    public static string? Tespit(ReadOnlySpan<byte> icerik)
    {
        if (Baslar(icerik, Png))
            return ".png";

        if (Baslar(icerik, Jpeg))
            return ".jpg";

        if (Baslar(icerik, Pdf))
            return ".pdf";

        // WEBP iki parcali: dosya "RIFF" ile baslar, 8. bayttan itibaren
        // "WEBP" gelir. Aradaki dort bayt dosya uzunlugu, degiskendir.
        if (Baslar(icerik, Riff) && icerik.Length >= 12 && icerik[8..12].SequenceEqual(Webp))
            return ".webp";

        return null;
    }

    /// <summary>Uzanti verilen listede mi. Karsilastirma kulturden bagimsiz.</summary>
    /// <param name="izinliler">
    /// Verilmezse gorsel listesi. Cagiran tarafin acikca liste vermesi,
    /// belge ucunun yanlislikla gorsel kurallariyla calismasini engelliyor.
    /// </param>
    public static bool UzantiIzinli(string? uzanti, IReadOnlyList<string>? izinliler = null) =>
        uzanti is not null &&
        (izinliler ?? AllowedExtensions).Contains(uzanti.ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>
    /// Icerikten tespit edilen tur ile dosya adindaki uzanti ayni dosya
    /// turune mi isaret ediyor.
    /// </summary>
    /// <remarks>
    /// <c>.jpeg</c> ile <c>.jpg</c> ayni turdur; tespit her zaman <c>.jpg</c>
    /// dondugu icin burada esitleniyor.
    /// </remarks>
    public static bool Eslesiyor(string tespitEdilen, string uzanti)
    {
        var normal = uzanti.ToLowerInvariant() switch
        {
            ".jpeg" => ".jpg",
            var digeri => digeri
        };

        return string.Equals(tespitEdilen, normal, StringComparison.Ordinal);
    }

    private static bool Baslar(ReadOnlySpan<byte> icerik, ReadOnlySpan<byte> imza) =>
        icerik.Length >= imza.Length && icerik[..imza.Length].SequenceEqual(imza);
}
