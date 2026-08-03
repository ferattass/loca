using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Rezervasyonun is kurallari.
/// </summary>
/// <remarks>
/// Sureler ve limit disaridan geliyor; testler de bu yuzden gercek
/// degerlerle degil kisa degerlerle calisabiliyor. On dakikalik kilit koda
/// gomulseydi "suresi dolmus rezervasyon" testi on dakika beklerdi.
/// </remarks>
public class ReservationTests
{
    private static readonly DateTime Simdi = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UzatmaSuresi = TimeSpan.FromMinutes(5);
    private const int Limit = 6;

    private static readonly Guid Kullanici = Guid.CreateVersion7();
    private static readonly Guid Oturum = Guid.CreateVersion7();

    private static ReservationLine Satir(decimal tutar = 450m, string birim = "TRY") =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), new Money(tutar, birim));

    private static Reservation Yeni(params ReservationLine[] satirlar) =>
        new(
            Kullanici,
            Oturum,
            "anahtar-1",
            satirlar.Length == 0 ? [Satir()] : satirlar,
            Simdi,
            KilitSuresi,
            Limit);

    [Fact]
    public void NewReservationShouldBePendingUntilDeadline()
    {
        var rezervasyon = Yeni();

        Assert.Equal(ReservationStatus.Pending, rezervasyon.Status);
        Assert.Equal(Simdi.Add(KilitSuresi), rezervasyon.ExpiresAtUtc);
        Assert.False(rezervasyon.ExtensionUsed);
        Assert.True(rezervasyon.IsActive(Simdi));
        Assert.False(rezervasyon.IsExpired(Simdi));
    }

    /// <remarks>
    /// Toplam istekten alinmaz, kalemlerden hesaplanir. Istemci tutar
    /// gonderseydi araya giren biri 450 TL'lik koltugu 1 TL'ye rezerve
    /// edebilirdi.
    /// </remarks>
    [Fact]
    public void TotalShouldBeSumOfLines()
    {
        var rezervasyon = Yeni(Satir(450m), Satir(200m), Satir(200m));

        Assert.Equal(850m, rezervasyon.TotalAmount.Amount);
        Assert.Equal("TRY", rezervasyon.TotalAmount.Currency);
        Assert.Equal(3, rezervasyon.SeatCount);
    }

    [Fact]
    public void SeatCountAboveLimitShouldThrow()
    {
        var satirlar = Enumerable.Range(0, Limit + 1).Select(_ => Satir()).ToArray();

        Assert.Throws<DomainException>(() => Yeni(satirlar));
    }

    /// <remarks>
    /// Ayni koltuk iki kez gonderilebilseydi kullanici limiti asmadan ayni
    /// koltuk icin iki kalem ve iki kat tutar olustururdu.
    /// </remarks>
    [Fact]
    public void DuplicateSeatShouldThrow()
    {
        var satir = Satir();

        Assert.Throws<DomainException>(() => Yeni(satir, satir));
    }

    [Fact]
    public void EmptySelectionShouldThrow() =>
        Assert.Throws<DomainException>(() =>
            new Reservation(Kullanici, Oturum, "anahtar", [], Simdi, KilitSuresi, Limit));

    [Fact]
    public void IdempotencyKeyIsRequired() =>
        Assert.Throws<DomainException>(() =>
            new Reservation(Kullanici, Oturum, "   ", [Satir()], Simdi, KilitSuresi, Limit));

    /// <remarks>
    /// Money farkli para birimlerinin toplanmasina izin vermiyor; karisik
    /// para birimli bir secim toplam hesaplanirken patlar.
    /// </remarks>
    [Fact]
    public void MixedCurrencyShouldThrow() =>
        Assert.Throws<DomainException>(() => Yeni(Satir(100m, "TRY"), Satir(100m, "USD")));

    [Fact]
    public void ExtendShouldPushDeadline()
    {
        var rezervasyon = Yeni();

        var yeniBitis = rezervasyon.Extend(Simdi.AddMinutes(9), UzatmaSuresi);

        Assert.Equal(Simdi.AddMinutes(15), yeniBitis);
        Assert.Equal(Simdi.AddMinutes(15), rezervasyon.ExpiresAtUtc);
        Assert.True(rezervasyon.ExtensionUsed);
    }

    /// <remarks>
    /// Sinirsiz uzatma, tek bir kullanicinin salonu kilitleyip satisi
    /// durdurmasina izin verirdi.
    /// </remarks>
    [Fact]
    public void SecondExtendShouldThrow()
    {
        var rezervasyon = Yeni();
        rezervasyon.Extend(Simdi.AddMinutes(1), UzatmaSuresi);

        Assert.Throws<DomainException>(() => rezervasyon.Extend(Simdi.AddMinutes(2), UzatmaSuresi));
    }

    /// <remarks>
    /// Suresi dolmus kilit uzatilmaz: koltuklar bu arada baskasina gitmis
    /// olabilir ve rezervasyon, artik tutmadigi koltuklarla ayakta kalirdi.
    /// </remarks>
    [Fact]
    public void ExpiredReservationCannotBeExtended()
    {
        var rezervasyon = Yeni();
        var sonra = Simdi.Add(KilitSuresi).AddSeconds(1);

        Assert.True(rezervasyon.IsExpired(sonra));
        Assert.Throws<DomainException>(() => rezervasyon.Extend(sonra, UzatmaSuresi));
    }

    [Fact]
    public void ExpiredReservationCannotBeConfirmed()
    {
        var rezervasyon = Yeni();
        var sonra = Simdi.Add(KilitSuresi).AddSeconds(1);

        Assert.Throws<DomainException>(() => rezervasyon.Confirm(sonra));
    }

    [Fact]
    public void CancelShouldCloseReservation()
    {
        var rezervasyon = Yeni();

        rezervasyon.Cancel(Simdi.AddMinutes(2));

        Assert.Equal(ReservationStatus.Cancelled, rezervasyon.Status);
        Assert.Equal(Simdi.AddMinutes(2), rezervasyon.CancelledAtUtc);
        Assert.False(rezervasyon.IsActive(Simdi.AddMinutes(2)));
    }

    /// <remarks>
    /// Odemesi alinmis rezervasyonun iptali iade akisi gerektirir; durumu
    /// buradan degistirmek parayi kullanicida birakmadan koltugu geri verirdi.
    /// </remarks>
    [Fact]
    public void ConfirmedReservationCannotBeCancelledHere()
    {
        var rezervasyon = Yeni();
        rezervasyon.Confirm(Simdi.AddMinutes(1));

        Assert.Throws<DomainException>(() => rezervasyon.Cancel(Simdi.AddMinutes(2)));
    }

    [Fact]
    public void CancelledReservationCannotBeCancelledTwice()
    {
        var rezervasyon = Yeni();
        rezervasyon.Cancel(Simdi.AddMinutes(1));

        Assert.Throws<DomainException>(() => rezervasyon.Cancel(Simdi.AddMinutes(2)));
    }

    [Fact]
    public void ExpireShouldOnlyWorkOnPending()
    {
        var rezervasyon = Yeni();
        rezervasyon.Cancel(Simdi.AddMinutes(1));

        Assert.Throws<DomainException>(rezervasyon.Expire);
    }

    [Fact]
    public void ConfirmedReservationStaysActiveAfterDeadline()
    {
        var rezervasyon = Yeni();
        rezervasyon.Confirm(Simdi.AddMinutes(1));

        var sonra = Simdi.Add(KilitSuresi).AddMinutes(1);

        // Odemesi tamamlanmis rezervasyonun kilit suresi anlamini yitirir:
        // koltuk artik satilmistir, geri alinmaz.
        Assert.True(rezervasyon.IsActive(sonra));
        Assert.False(rezervasyon.IsExpired(sonra));
    }

    /// <remarks>
    /// Geri sayim sunucuda hesaplanir; suresi gecmis bir kayitta negatif
    /// deger donmemeli, yoksa arayuz eksi saniye gosterirdi.
    /// </remarks>
    [Fact]
    public void RemainingTimeShouldNotGoNegative()
    {
        var rezervasyon = Yeni();

        Assert.Equal(TimeSpan.FromMinutes(4), rezervasyon.RemainingTime(Simdi.AddMinutes(6)));
        Assert.Equal(TimeSpan.Zero, rezervasyon.RemainingTime(Simdi.AddMinutes(30)));
    }
}
