namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Rezervasyonun sayisal is kurallari.
/// </summary>
/// <remarks>
/// Bu degerler koda gomulmuyor cunku testte kisaltilabilmeleri gerekiyor:
/// on dakikalik kilidin dolmasini bekleyen bir kabul testi on dakika
/// calisirdi. Degerler <c>.env</c> ve <c>appsettings</c> uzerinden geliyor,
/// uygulama katmani yalnizca bu arayuzu goruyor.
/// </remarks>
public interface IReservationPolicy
{
    /// <summary>Koltugun kilitli kalacagi sure. Varsayilan 10 dakika.</summary>
    TimeSpan LockDuration { get; }

    /// <summary>Uzatmada eklenen sure. Varsayilan 5 dakika, en fazla bir kez.</summary>
    TimeSpan ExtendDuration { get; }

    /// <summary>Bir kullanicinin tek oturumda alabilecegi en fazla bilet. Varsayilan 6.</summary>
    int MaxSeatsPerUserPerSession { get; }
}
