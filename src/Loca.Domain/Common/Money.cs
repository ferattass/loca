using System.Globalization;

namespace Loca.Domain.Common;

/// <summary>
/// Para tutari ve birimi. Deger nesnesi oldugu icin esitlik
/// referansa degil icerige gore calisir.
/// </summary>
/// <remarks>
/// decimal kullanilir, float/double ASLA kullanilmaz. Ikilik kayan nokta
/// ondalik sayiyi tam temsil edemez (0.1 + 0.2 = 0.30000000000000004);
/// yuz bin biletlik bir satista bu hata birikir ve muhasebe tutmaz.
/// </remarks>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Tutar negatif olamaz.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Para birimi 3 harfli ISO kodu olmali.");

        // Bankaci yuvarlamasi: 0,5 durumunda cift sayiya yuvarlar.
        // Cok sayida islemde sistematik sapmayi onler.
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity) =>
        new(money.Amount * quantity, money.Currency);

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
            throw new DomainException(
                $"Farkli para birimleri islenemez: {left.Currency} ve {right.Currency}.");
    }

    public override string ToString() =>
        Amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " " + Currency;
}
