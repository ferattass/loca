using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Etkinligin durum gecisleri.
/// </summary>
/// <remarks>
/// Bu kurallar handler'da degil entity'de duruyor; testler de bu yuzden
/// veritabani olmadan calisabiliyor. Bir gecis yanlislikla serbest
/// birakilirsa buradaki testler kirilir — ornegin taslak bir etkinlik
/// dogrudan yayina alinabilir hale gelirse.
/// </remarks>
public class EventStateTests
{
    private static readonly DateTime Simdi = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Event YeniEtkinlik() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Yaz Konseri", "Aciklama");

    /// <summary>Yayina hazir etkinlik: bir oturum, bir bilet turu ve afis.</summary>
    private static Event HazirEtkinlik()
    {
        var etkinlik = YeniEtkinlik();

        etkinlik.AddSession(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Simdi.AddDays(30),
            Simdi.AddDays(30).AddHours(2),
            Simdi,
            Simdi.AddDays(29));

        etkinlik.AddTicketType(
            "Tam", new Money(500, "TRY"), 100, Simdi, Simdi.AddDays(29));

        etkinlik.SetPoster(Guid.CreateVersion7());

        return etkinlik;
    }

    [Fact]
    public void NewEventShouldStartAsDraft()
    {
        Assert.Equal(EventStatus.Draft, YeniEtkinlik().Status);
    }

    [Fact]
    public void DraftCannotBePublishedDirectly()
    {
        var etkinlik = HazirEtkinlik();

        // Onay adimi atlanamaz: admin gormeden yayina cikan etkinlik
        // moderasyon zincirini delerdi.
        Assert.Throws<DomainException>(() => etkinlik.Publish(Simdi));
    }

    [Fact]
    public void SubmitForApprovalRequiresAtLeastOneSession()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddTicketType("Tam", new Money(500, "TRY"), 100, Simdi, Simdi.AddDays(29));
        etkinlik.SetPoster(Guid.CreateVersion7());

        var hata = Assert.Throws<DomainException>(etkinlik.SubmitForApproval);

        Assert.Contains("oturum", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmitForApprovalRequiresAtLeastOneTicketType()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));
        etkinlik.SetPoster(Guid.CreateVersion7());

        var hata = Assert.Throws<DomainException>(etkinlik.SubmitForApproval);

        Assert.Contains("bilet turu", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmitForApprovalRequiresPoster()
    {
        var etkinlik = YeniEtkinlik();
        etkinlik.AddSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));
        etkinlik.AddTicketType("Tam", new Money(500, "TRY"), 100, Simdi, Simdi.AddDays(29));

        var hata = Assert.Throws<DomainException>(etkinlik.SubmitForApproval);

        Assert.Contains("afis", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullLifecycleShouldReachCompleted()
    {
        var etkinlik = HazirEtkinlik();

        etkinlik.SubmitForApproval();
        Assert.Equal(EventStatus.PendingApproval, etkinlik.Status);

        etkinlik.Publish(Simdi);
        Assert.Equal(EventStatus.Published, etkinlik.Status);
        Assert.Equal(Simdi, etkinlik.PublishedAt);

        etkinlik.OpenSales();
        Assert.Equal(EventStatus.SalesOpen, etkinlik.Status);

        etkinlik.CloseSales();
        etkinlik.Complete();

        Assert.Equal(EventStatus.Completed, etkinlik.Status);
    }

    [Fact]
    public void CriticalChangesShouldBeLockedAfterPublish()
    {
        var etkinlik = HazirEtkinlik();

        Assert.True(etkinlik.AllowsCriticalChanges);

        etkinlik.SubmitForApproval();
        etkinlik.Publish(Simdi);

        // Yayindan sonra salon veya tarih sessizce degisirse bilet almis
        // kullanici baska bir salona gider.
        Assert.False(etkinlik.AllowsCriticalChanges);
    }

    [Fact]
    public void CompletedEventCannotBeCancelled()
    {
        var etkinlik = HazirEtkinlik();
        etkinlik.SubmitForApproval();
        etkinlik.Publish(Simdi);
        etkinlik.OpenSales();
        etkinlik.Complete();

        Assert.Throws<DomainException>(() => etkinlik.Cancel(Simdi, "Sanatci hastalandi"));
    }

    [Fact]
    public void CancellationRequiresReason()
    {
        var etkinlik = HazirEtkinlik();

        Assert.Throws<DomainException>(() => etkinlik.Cancel(Simdi, "   "));
    }

    [Fact]
    public void CancellingEventShouldCancelItsSessions()
    {
        var etkinlik = HazirEtkinlik();

        etkinlik.Cancel(Simdi, "Sanatci hastalandi");

        Assert.Equal(EventStatus.Cancelled, etkinlik.Status);
        Assert.All(
            etkinlik.Sessions,
            session => Assert.Equal(EventSessionStatus.Cancelled, session.Status));
    }

    [Fact]
    public void SuspendedEventShouldResumeToPublished()
    {
        var etkinlik = HazirEtkinlik();
        etkinlik.SubmitForApproval();
        etkinlik.Publish(Simdi);
        etkinlik.OpenSales();

        etkinlik.Suspend();
        Assert.Equal(EventStatus.Suspended, etkinlik.Status);

        // Askidan donunce satis DEGIL yayin durumuna donulur: satisin
        // yeniden acilmasi ayri ve bilincli bir karar olmali.
        etkinlik.Resume();
        Assert.Equal(EventStatus.Published, etkinlik.Status);
    }

    /// <remarks>
    /// Ayni salonda, temizlik payi icinde kalan ikinci oturum reddedilmeli.
    /// Kabul edilseydi bir seans biterken digeri baslar ve ayni salonda iki
    /// kalabalik karsi karsiya gelirdi.
    /// </remarks>
    [Fact]
    public void OverlappingSessionInSameHallShouldBeRejected()
    {
        var etkinlik = YeniEtkinlik();
        var salon = Guid.CreateVersion7();
        var plan = Guid.CreateVersion7();

        etkinlik.AddSession(
            salon, plan,
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));

        // Ilk oturum 14:00'te bitiyor, temizlik payiyla 15:00'e kadar mesgul.
        // 14:30'da baslayan oturum cakisir.
        var hata = Assert.Throws<DomainException>(() => etkinlik.AddSession(
            salon, plan,
            Simdi.AddDays(30).AddHours(2).AddMinutes(30),
            Simdi.AddDays(30).AddHours(4),
            Simdi,
            Simdi.AddDays(29)));

        Assert.Contains("temizlik payi", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionInSameHallAfterCleanupGapShouldBeAccepted()
    {
        var etkinlik = YeniEtkinlik();
        var salon = Guid.CreateVersion7();
        var plan = Guid.CreateVersion7();

        etkinlik.AddSession(
            salon, plan,
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));

        etkinlik.AddSession(
            salon, plan,
            Simdi.AddDays(30).AddHours(4),
            Simdi.AddDays(30).AddHours(6),
            Simdi,
            Simdi.AddDays(29));

        Assert.Equal(2, etkinlik.Sessions.Count);
    }

    [Fact]
    public void DifferentHallsShouldNotConflict()
    {
        var etkinlik = YeniEtkinlik();
        var plan = Guid.CreateVersion7();

        etkinlik.AddSession(
            Guid.CreateVersion7(), plan,
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));

        etkinlik.AddSession(
            Guid.CreateVersion7(), plan,
            Simdi.AddDays(30), Simdi.AddDays(30).AddHours(2), Simdi, Simdi.AddDays(29));

        Assert.Equal(2, etkinlik.Sessions.Count);
    }
}
