using Loca.Domain.Common;
using Loca.Domain.Entities;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Bilet turu - koltuk bolumu atamasi ve etkinligin tarih tutarliligi.
/// </summary>
/// <remarks>
/// Sartnamenin kurali: <b>ayni koltuk birden fazla aktif bilet turune
/// atanamaz.</b> Atama bolum bazinda yapildigi icin kural "ayni bolum iki
/// aktif ture atanamaz" hâline geliyor ve aggregate icinde, veritabanina
/// gitmeden dogrulanabiliyor.
/// </remarks>
public class TicketTypeSectionTests
{
    private static readonly DateTime Simdi = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Baslangic = Simdi.AddDays(30);

    private static Event YeniEtkinlik() =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Yaz Konseri",
            "Aciklama",
            new EventPlace(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()),
            new EventSchedule(Baslangic, 120, Simdi, Simdi.AddDays(29)),
            "Etkinlikten 24 saat oncesine kadar tam iade.");

    private static TicketType TurEkle(Event etkinlik, string ad, Guid? bolum = null) =>
        etkinlik.AddTicketType(
            ad, new Money(450, "TRY"), 100, Simdi, Simdi.AddDays(29), false, bolum);

    [Fact]
    public void DuplicateTicketTypeNameShouldBeRejected()
    {
        var etkinlik = YeniEtkinlik();
        TurEkle(etkinlik, "Tam");

        var hata = Assert.Throws<DomainException>(() => TurEkle(etkinlik, "Tam"));

        Assert.Contains("zaten var", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <remarks>Buyuk-kucuk harf farki ayri bir tur sayilmamali.</remarks>
    [Fact]
    public void TicketTypeNameComparisonShouldIgnoreCase()
    {
        var etkinlik = YeniEtkinlik();
        TurEkle(etkinlik, "Tam");

        Assert.Throws<DomainException>(() => TurEkle(etkinlik, "TAM"));
    }

    [Fact]
    public void SameSectionShouldNotBeAssignedToTwoActiveTypes()
    {
        var etkinlik = YeniEtkinlik();
        var balkon = Guid.CreateVersion7();

        TurEkle(etkinlik, "Balkon", balkon);
        var ikinci = TurEkle(etkinlik, "Balkon VIP");

        var hata = Assert.Throws<DomainException>(() =>
            etkinlik.AssignTicketTypeToSection(ikinci.Id, balkon));

        Assert.Contains("aktif bilet turune", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// Kural AKTIF turlerle sinirli: pasifleştirilmis bir tur bolumu
    /// bloklamaya devam etseydi organizator hatali bir turu silip yerine
    /// yenisini koyamazdi.
    /// </remarks>
    [Fact]
    public void SectionShouldBeReassignableAfterFirstTypeIsDeactivated()
    {
        var etkinlik = YeniEtkinlik();
        var balkon = Guid.CreateVersion7();

        var ilk = TurEkle(etkinlik, "Balkon", balkon);
        var ikinci = TurEkle(etkinlik, "Balkon VIP");

        ilk.Deactivate();
        etkinlik.AssignTicketTypeToSection(ikinci.Id, balkon);

        Assert.Equal(balkon, ikinci.SeatSectionId);
    }

    [Fact]
    public void AssigningNullShouldMakeTypeDefault()
    {
        var etkinlik = YeniEtkinlik();
        var balkon = Guid.CreateVersion7();
        var tur = TurEkle(etkinlik, "Balkon", balkon);

        etkinlik.AssignTicketTypeToSection(tur.Id, null);

        Assert.Null(tur.SeatSectionId);
        Assert.Equal(tur, etkinlik.DefaultTicketType);
    }

    [Fact]
    public void ForeignTicketTypeShouldBeRejected()
    {
        var etkinlik = YeniEtkinlik();

        Assert.Throws<DomainException>(() =>
            etkinlik.AssignTicketTypeToSection(Guid.CreateVersion7(), null));
    }

    /// <remarks>
    /// Bolume atanmamis aktif tur yoksa eslesmeyen bolumun koltuklari
    /// fiyatsiz kalir; uretim bu durumda hata vermeli.
    /// </remarks>
    [Fact]
    public void DefaultTicketTypeShouldBeNullWhenEveryTypeIsAssigned()
    {
        var etkinlik = YeniEtkinlik();
        TurEkle(etkinlik, "Balkon", Guid.CreateVersion7());

        Assert.Null(etkinlik.DefaultTicketType);
    }

    [Fact]
    public void DeactivatedTypeShouldNotBeDefault()
    {
        var etkinlik = YeniEtkinlik();
        var tur = TurEkle(etkinlik, "Tam");

        tur.Deactivate();

        Assert.Null(etkinlik.DefaultTicketType);
    }

    // --- Tarih tutarliligi -------------------------------------------------

    /// <remarks>
    /// Iki ayri tarih tutuldugu anda kacinilmaz soru: hangisi dogru? Cevap
    /// "en erken oturum" — kullanicinin listede gordugu tarih perdenin
    /// gercekten acildigi andir.
    /// </remarks>
    [Fact]
    public void EventDateShouldAlignToEarliestSession()
    {
        var etkinlik = YeniEtkinlik();
        var salon = Guid.CreateVersion7();
        var plan = Guid.CreateVersion7();

        etkinlik.AddSession(
            salon, plan, Baslangic.AddDays(2), Baslangic.AddDays(2).AddHours(2),
            Simdi, Simdi.AddDays(29));

        Assert.Equal(Baslangic.AddDays(2), etkinlik.EventDateUtc);

        // Daha erken bir oturum eklenince tarih ONA cekilmeli.
        etkinlik.AddSession(
            Guid.CreateVersion7(), plan, Baslangic, Baslangic.AddHours(2),
            Simdi, Simdi.AddDays(29));

        Assert.Equal(Baslangic, etkinlik.EventDateUtc);
    }

    /// <remarks>
    /// Bilet hâlâ satiliyorken perdenin acilmasi anlamsiz. Ayrica bu kural
    /// olmadan tarih hizalamasi etkinlik tarihini satis bitisinin oncesine
    /// cekip <see cref="EventSchedule"/> kuralini bozabilirdi.
    /// </remarks>
    [Fact]
    public void SessionStartingBeforeSalesEndShouldBeRejected()
    {
        var etkinlik = YeniEtkinlik();

        var hata = Assert.Throws<DomainException>(() => etkinlik.AddSession(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Simdi.AddDays(28),
            Simdi.AddDays(28).AddHours(2),
            Simdi,
            Simdi.AddDays(27)));

        Assert.Contains("satisi kapanmadan once", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RescheduleShouldBeRejectedWhenSessionsExist()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Baslangic, Baslangic.AddHours(2), Simdi, Simdi.AddDays(29));

        var hata = Assert.Throws<DomainException>(() => etkinlik.Reschedule(
            new EventSchedule(Baslangic.AddDays(1), 120, Simdi, Simdi.AddDays(29))));

        Assert.Contains("once oturumun tarihini", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RescheduleShouldBeAllowedWithoutSessions()
    {
        var etkinlik = YeniEtkinlik();

        etkinlik.Reschedule(new EventSchedule(Baslangic.AddDays(1), 180, Simdi, Simdi.AddDays(29)));

        Assert.Equal(Baslangic.AddDays(1), etkinlik.EventDateUtc);
        Assert.Equal(180, etkinlik.DurationMinutes);
    }

    [Fact]
    public void PlaceShouldBeLockedAfterPublish()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Baslangic, Baslangic.AddHours(2), Simdi, Simdi.AddDays(29));
        TurEkle(etkinlik, "Tam");
        etkinlik.SetPoster(Guid.CreateVersion7());

        etkinlik.SubmitForApproval();
        etkinlik.Publish(Simdi);

        var yeniYer = new EventPlace(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Throws<DomainException>(() => etkinlik.ChangePlace(yeniYer));
    }

    /// <remarks>
    /// Bilet turu var ama hepsi pasifse etkinlik listede gorunur ve
    /// kullanici bilet alamaz — hata vermeyen, yalnizca ise yaramayan kayit.
    /// </remarks>
    [Fact]
    public void PublishShouldRequireAtLeastOneActiveTicketType()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Baslangic, Baslangic.AddHours(2), Simdi, Simdi.AddDays(29));
        var tur = TurEkle(etkinlik, "Tam");
        etkinlik.SetPoster(Guid.CreateVersion7());

        tur.Deactivate();

        var hata = Assert.Throws<DomainException>(etkinlik.SubmitForApproval);

        Assert.Contains("aktif bilet turu", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// Oturum sayisi sifir degil ama hepsi iptalse satin alinabilecek hicbir
    /// seans yok.
    /// </remarks>
    [Fact]
    public void PublishShouldRequireNonCancelledSession()
    {
        var etkinlik = YeniEtkinlik();
        var oturum = etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Baslangic, Baslangic.AddHours(2), Simdi, Simdi.AddDays(29));
        TurEkle(etkinlik, "Tam");
        etkinlik.SetPoster(Guid.CreateVersion7());

        oturum.Cancel();

        var hata = Assert.Throws<DomainException>(etkinlik.SubmitForApproval);

        Assert.Contains("iptal edilmemis", hata.Message, StringComparison.OrdinalIgnoreCase);
    }
}
