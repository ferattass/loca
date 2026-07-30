using Loca.Domain.Common;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Sartnamenin uc tarih kurali.
/// </summary>
/// <remarks>
/// Bu kurallar handler'da degil deger nesnesinde duruyor; testler de bu
/// yuzden veritabani olmadan calisiyor. Kural gevsetilirse — ornegin satis
/// bitisi etkinlik baslangicindan sonraya alinabilir hale gelirse — buradaki
/// testler kirilir.
/// </remarks>
public class EventScheduleTests
{
    private static readonly DateTime Baslangic = new(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc);

    private static EventSchedule Gecerli() =>
        new(Baslangic, 120, Baslangic.AddDays(-45), Baslangic.AddHours(-6));

    [Fact]
    public void ValidScheduleShouldBeAccepted()
    {
        var plan = Gecerli();

        Assert.Equal(Baslangic, plan.EventDateUtc);
        Assert.Equal(120, plan.DurationMinutes);
    }

    /// <remarks>
    /// "Bitis tarihi baslangic tarihinden once olamaz" kurali veriyle degil
    /// hesapla garanti ediliyor: sure pozitif oldugu icin bitis her zaman
    /// baslangictan sonra.
    /// </remarks>
    [Fact]
    public void EndsAtShouldBeStartPlusDuration()
    {
        Assert.Equal(Baslangic.AddMinutes(120), Gecerli().EndsAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void NonPositiveDurationShouldBeRejected(int dakika)
    {
        Assert.Throws<DomainException>(() =>
            new EventSchedule(Baslangic, dakika, Baslangic.AddDays(-45), Baslangic.AddHours(-6)));
    }

    /// <remarks>
    /// Ust sinir olmasa parmak hatasiyla girilen 6000 dakika sessizce kabul
    /// edilir ve salon cakisma kontrolu gunler boyu her oturumu reddederdi.
    /// </remarks>
    [Fact]
    public void DurationAboveDailyLimitShouldBeRejected()
    {
        Assert.Throws<DomainException>(() => new EventSchedule(
            Baslangic,
            EventSchedule.MaxDurationMinutes + 1,
            Baslangic.AddDays(-45),
            Baslangic.AddHours(-6)));
    }

    [Fact]
    public void SalesEndBeforeSalesStartShouldBeRejected()
    {
        var hata = Assert.Throws<DomainException>(() => new EventSchedule(
            Baslangic, 120, Baslangic.AddHours(-6), Baslangic.AddDays(-45)));

        Assert.Contains("satis baslangicindan sonra", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// Etkinlik basladiktan sonra bilet satmanin karsiligi yok: seyirci iceri
    /// girmis, koltuklar dolmus olur.
    /// </remarks>
    [Fact]
    public void SalesEndingAfterEventStartShouldBeRejected()
    {
        var hata = Assert.Throws<DomainException>(() => new EventSchedule(
            Baslangic, 120, Baslangic.AddDays(-45), Baslangic.AddMinutes(1)));

        Assert.Contains("etkinlik baslamadan once bitmeli", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesEndingExactlyAtEventStartShouldBeAccepted()
    {
        var plan = new EventSchedule(Baslangic, 120, Baslangic.AddDays(-45), Baslangic);

        Assert.Equal(Baslangic, plan.SalesEndsAtUtc);
    }

    [Fact]
    public void SalesWindowShouldBeOpenOnlyInsideRange()
    {
        var plan = Gecerli();

        Assert.False(plan.IsSalesWindowOpen(plan.SalesStartsAtUtc.AddMinutes(-1)));
        Assert.True(plan.IsSalesWindowOpen(plan.SalesStartsAtUtc));
        Assert.False(plan.IsSalesWindowOpen(plan.SalesEndsAtUtc));
    }

    [Fact]
    public void WithEventDateShouldKeepSalesWindow()
    {
        var plan = Gecerli();
        var tasinmis = plan.WithEventDate(Baslangic.AddDays(1));

        Assert.Equal(Baslangic.AddDays(1), tasinmis.EventDateUtc);
        Assert.Equal(plan.SalesStartsAtUtc, tasinmis.SalesStartsAtUtc);
        Assert.Equal(plan.SalesEndsAtUtc, tasinmis.SalesEndsAtUtc);
    }

    /// <remarks>
    /// Uc kimlik de <c>Guid</c> oldugu icin cagri yerinde siralarinin
    /// karismasi derleyiciye gorunmez; en azindan bos olmadiklari garanti.
    /// </remarks>
    [Fact]
    public void PlaceShouldRejectEmptyIdentifiers()
    {
        var dolu = Guid.CreateVersion7();

        Assert.Throws<DomainException>(() => new EventPlace(Guid.Empty, dolu, dolu));
        Assert.Throws<DomainException>(() => new EventPlace(dolu, Guid.Empty, dolu));
        Assert.Throws<DomainException>(() => new EventPlace(dolu, dolu, Guid.Empty));
    }
}
