using Loca.Domain.Common;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Para tutarinin kurallari. Bu kurallar bozulursa hata muhasebede
/// ortaya cikar ve geriye donuk duzeltmesi zordur; bu yuzden testle sabitlendi.
/// </summary>
public class MoneyTests
{
    [Fact]
    public void NegativeAmountShouldBeRejected()
    {
        var exception = Assert.Throws<DomainException>(() => new Money(-1, "TRY"));

        Assert.Contains("negatif", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TR")]
    [InlineData("TRYX")]
    public void CurrencyMustBeThreeLetterCode(string currency)
    {
        Assert.Throws<DomainException>(() => new Money(10, currency));
    }

    [Fact]
    public void CurrencyShouldBeStoredInUpperCase()
    {
        var money = new Money(10, "try");

        Assert.Equal("TRY", money.Currency);
    }

    /// <remarks>
    /// Bankaci yuvarlamasi 0,5 durumunda cift sayiya gider. Her seferinde
    /// yukari yuvarlanirsa cok sayida islemde tutar sistematik olarak sisier.
    /// </remarks>
    [Theory]
    [InlineData(2.005, 2.00)]
    [InlineData(2.015, 2.02)]
    [InlineData(2.025, 2.02)]
    public void AmountShouldUseBankersRounding(decimal input, decimal expected)
    {
        var money = new Money(input, "TRY");

        Assert.Equal(expected, money.Amount);
    }

    [Fact]
    public void DifferentCurrenciesCannotBeAdded()
    {
        var tl = new Money(100, "TRY");
        var dollar = new Money(100, "USD");

        Assert.Throws<DomainException>(() => tl + dollar);
    }

    [Fact]
    public void SubtractionBelowZeroShouldBeRejected()
    {
        var balance = new Money(50, "TRY");
        var refund = new Money(80, "TRY");

        Assert.Throws<DomainException>(() => balance - refund);
    }

    [Fact]
    public void MultiplicationShouldScaleAmount()
    {
        var ticketPrice = new Money(149.90m, "TRY");

        var total = ticketPrice * 3;

        Assert.Equal(449.70m, total.Amount);
        Assert.Equal("TRY", total.Currency);
    }

    /// <remarks>
    /// Deger nesnesi oldugu icin esitlik referansa degil icerige bakar.
    /// Iki ayri nesne ayni tutar ve birimi tasiyorsa esittir.
    /// </remarks>
    [Fact]
    public void EqualityShouldCompareValuesNotReferences()
    {
        var first = new Money(25.50m, "TRY");
        var second = new Money(25.50m, "try");

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void ZeroShouldCarryCurrency()
    {
        var zero = Money.Zero("TRY");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("TRY", zero.Currency);
    }
}
