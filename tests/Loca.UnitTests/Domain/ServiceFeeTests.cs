using Loca.Domain.Common;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Hizmet bedeli hesabi.
/// </summary>
/// <remarks>
/// Saf mantik: veritabani, saat ve HTTP olmadan sinanabiliyor. Para
/// hesabinin testi ozellikle onemli cunku bir kurusluk sapma her satista
/// tekrarlaniyor ve mutabakatta ancak aylar sonra fark ediliyor.
/// </remarks>
public sealed class ServiceFeeTests
{
    private static Money Lira(decimal tutar) => new(tutar, "TRY");

    [Fact]
    public void ZeroPolicyAddsNothing()
    {
        var bedel = ServiceFeePolicy.None.Calculate(Lira(1000m), 2);

        Assert.Equal(Lira(0m), bedel);
    }

    [Fact]
    public void PercentAppliesToItemsTotal()
    {
        var politika = new ServiceFeePolicy(percent: 8m, minimumPerTicket: 0m);

        Assert.Equal(Lira(80m), politika.Calculate(Lira(1000m), 1));
    }

    /// <summary>
    /// Ucuz bilette alt sinir devreye giriyor.
    /// </summary>
    /// <remarks>
    /// Yalnizca yuzde olsaydi bes liralik ogrenci biletinden kirk kurus
    /// alinirdi; odeme saglayicisinin islem basina aldigi ucret bile bunun
    /// ustunde ve platform her ucuz bilette zarar ederdi.
    /// </remarks>
    [Fact]
    public void MinimumWinsOnCheapTickets()
    {
        var politika = new ServiceFeePolicy(percent: 8m, minimumPerTicket: 5m);

        // %8 → 0,40 TL; alt sinir → 5 TL. Buyugu aliniyor.
        Assert.Equal(Lira(5m), politika.Calculate(Lira(5m), 1));
    }

    /// <remarks>
    /// Alt sinir bilet BASINA: uc koltuk alan biri tek koltuk alandan uc kat
    /// islem maliyeti uretmiyor olabilir ama uc ayri bilet uretiliyor ve
    /// ucunun de kapida okutulmasi gerekiyor.
    /// </remarks>
    [Fact]
    public void MinimumIsPerTicketNotPerReservation()
    {
        var politika = new ServiceFeePolicy(percent: 1m, minimumPerTicket: 5m);

        // %1 → 3 TL; alt sinir → 3 bilet x 5 = 15 TL.
        Assert.Equal(Lira(15m), politika.Calculate(Lira(300m), 3));
    }

    [Fact]
    public void PercentWinsOnExpensiveTickets()
    {
        var politika = new ServiceFeePolicy(percent: 8m, minimumPerTicket: 5m);

        // %8 → 80 TL; alt sinir → 5 TL.
        Assert.Equal(Lira(80m), politika.Calculate(Lira(1000m), 1));
    }

    [Fact]
    public void CurrencyFollowsItemsTotal()
    {
        var politika = new ServiceFeePolicy(percent: 10m, minimumPerTicket: 0m);

        Assert.Equal("USD", politika.Calculate(new Money(100m, "USD"), 1).Currency);
    }

    /// <remarks>
    /// Yazim hatasi (8 yerine 800) burada yakalaniyor; yakalanmasaydi
    /// musteri biletin dokuz katini odeme ekraninda gorurdu.
    /// </remarks>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidPercentThrows(decimal yuzde) =>
        Assert.Throws<DomainException>(() => new ServiceFeePolicy(yuzde, 0m));

    [Fact]
    public void NegativeMinimumThrows() =>
        Assert.Throws<DomainException>(() => new ServiceFeePolicy(8m, -1m));

    [Fact]
    public void ZeroTicketCountThrows()
    {
        var politika = new ServiceFeePolicy(8m, 5m);

        Assert.Throws<DomainException>(() => politika.Calculate(Lira(100m), 0));
    }
}
