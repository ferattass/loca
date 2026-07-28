namespace Loca.Domain.Common;

/// <summary>
/// Is kurali ihlali. Ornegin gecersiz bir durum gecisi denendiginde firlatilir.
/// Web katmaninda 409 Conflict + Problem Details olarak karsilanir.
/// </summary>
/// <remarks>
/// Beklenen is kurali hatalari icin <c>Result</c> tercih edilir; bu tur
/// yalnizca domain metotlarinin ic tutarliligini korumak icindir. Bir entity
/// metodu kendi kuralini ihlal edecek bir cagriyi sessizce kabul etmemelidir.
/// </remarks>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }

    public DomainException() : base("Is kurali ihlal edildi.") { }
}
