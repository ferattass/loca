namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Sistem saati. Kod icinde <c>DateTime.UtcNow</c> dogrudan cagrilmaz.
/// </summary>
/// <remarks>
/// Analiz belgesi 3.16: "Sunucuda DateTime.Now kullanilmayacak; zaman bir
/// arayuz uzerinden alinacak ki testte sabitlenebilsin."
///
/// <para>
/// Bunun somut karsiligi Gun 6'da gorulecek: koltuk kilidinin 10 dakika sonra
/// dustugunu dogrulamak icin testin 10 dakika beklemesi gerekmesin, saati
/// ileri alsin yeter.
/// </para>
/// </remarks>
public interface IDateTimeProvider
{
    /// <summary>Her zaman UTC. Yerel saate cevirme isi gosterim katmanindadir.</summary>
    DateTime UtcNow { get; }
}
