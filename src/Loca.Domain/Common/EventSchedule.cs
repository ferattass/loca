namespace Loca.Domain.Common;

/// <summary>
/// Etkinligin tarih kumesi: ne zaman, ne kadar surecek, bileti ne zaman satilacak.
/// </summary>
/// <remarks>
/// Sartnamenin uc tarih kurali burada tek yerde duruyor. Bu uc alan Event
/// icinde ayri ayri dolasirsa "satis, etkinlik baslamadan once bitmeli"
/// kurali her yeni endpoint'te yeniden yazilmak zorunda kalir; biri
/// unuttugunda etkinlik basladiktan sonra bilet satilabilir hale gelir.
///
/// <para>
/// Deger nesnesi olmasina ragmen veritabanina owned type olarak DEGIL, duz
/// kolonlar olarak aciliyor: ana listeleme sorgusu
/// <c>(CityId, CategoryId, EventDate)</c> bilesik index'ini kullaniyor ve
/// owned type icindeki bir alan bilesik index'e giremez. Yani burada
/// dogrulama toplaniyor, saklama bicimi entity'de duz kaliyor.
/// </para>
/// </remarks>
public readonly record struct EventSchedule
{
    /// <summary>
    /// Bir etkinligin en uzun makul suresi.
    /// </summary>
    /// <remarks>
    /// Ust sinir olmasa parmak hatasiyla girilen 6000 dakika sessizce kabul
    /// edilir ve salon cakisma kontrolu gunler boyu her oturumu reddederdi.
    /// </remarks>
    public const int MaxDurationMinutes = 24 * 60;

    public EventSchedule(
        DateTime eventDateUtc,
        int durationMinutes,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc)
    {
        if (durationMinutes <= 0)
            throw new DomainException("Etkinlik suresi sifirdan buyuk olmali.");

        if (durationMinutes > MaxDurationMinutes)
            throw new DomainException($"Etkinlik suresi en fazla {MaxDurationMinutes} dakika olabilir.");

        // Sartname: "Satis baslangic tarihi satis bitis tarihinden sonra olamaz."
        if (salesEndsAtUtc <= salesStartsAtUtc)
            throw new DomainException("Satis bitisi satis baslangicindan sonra olmali.");

        // Sartname: "Satis bitis tarihi etkinlik baslangicindan sonra olamaz."
        // Etkinlik basladiktan sonra bilet satmanin karsiligi yok: seyirci
        // iceri girmis, koltuklar dolmus olur.
        if (salesEndsAtUtc > eventDateUtc)
            throw new DomainException("Bilet satisi etkinlik baslamadan once bitmeli.");

        EventDateUtc = eventDateUtc;
        DurationMinutes = durationMinutes;
        SalesStartsAtUtc = salesStartsAtUtc;
        SalesEndsAtUtc = salesEndsAtUtc;
    }

    /// <summary>Etkinligin duyurulan baslangic ani.</summary>
    public DateTime EventDateUtc { get; }

    public int DurationMinutes { get; }

    public DateTime SalesStartsAtUtc { get; }
    public DateTime SalesEndsAtUtc { get; }

    /// <summary>
    /// Etkinligin bitis ani.
    /// </summary>
    /// <remarks>
    /// "Bitis tarihi baslangic tarihinden once olamaz" kurali icin ayri bir
    /// kontrol yok: sure pozitif oldugu icin bitis her zaman baslangictan
    /// sonra. Kural veriyle degil hesapla garanti ediliyor.
    /// </remarks>
    public DateTime EndsAtUtc => EventDateUtc.AddMinutes(DurationMinutes);

    /// <summary>Verilen anda bilet satisi acik mi.</summary>
    public bool IsSalesWindowOpen(DateTime utcNow) =>
        utcNow >= SalesStartsAtUtc && utcNow < SalesEndsAtUtc;

    /// <summary>Etkinligin baslangicini kaydirir, satis penceresini korur.</summary>
    public EventSchedule WithEventDate(DateTime eventDateUtc) =>
        new(eventDateUtc, DurationMinutes, SalesStartsAtUtc, SalesEndsAtUtc);
}
