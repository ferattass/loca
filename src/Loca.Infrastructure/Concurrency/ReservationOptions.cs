using System.ComponentModel.DataAnnotations;
using Loca.Application.Common.Interfaces;

namespace Loca.Infrastructure.Concurrency;

/// <summary>
/// Rezervasyon is kurallarinin yapilandirilabilir sayilari.
/// </summary>
/// <remarks>
/// Kaynak: <c>.env</c> icindeki <c>RESERVATION_LOCK_MINUTES</c>,
/// <c>RESERVATION_EXTEND_MINUTES</c>, <c>MAX_SEATS_PER_USER_PER_SESSION</c>.
/// Kararlarin gerekceleri <c>docs/01-analiz.md</c> dosyasinda.
/// </remarks>
public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    /// <summary>
    /// Kilit suresi (dakika).
    /// </summary>
    /// <remarks>
    /// Ust sinir 60: daha uzun bir kilit, odemeyi yarim birakan tek bir
    /// kullanicinin koltugu bir saatten fazla bloke etmesi demek olurdu.
    /// Alt sinir 1 degil cunku saniyelik degerler kabul testinde kullaniliyor
    /// — ama dakika biriminde en kucuk anlamli deger yine 1.
    /// </remarks>
    [Range(1, 60)]
    public int LockMinutes { get; set; } = 10;

    [Range(1, 30)]
    public int ExtendMinutes { get; set; } = 5;

    [Range(1, 20)]
    public int MaxSeatsPerUserPerSession { get; set; } = 6;

    /// <summary>
    /// Suresi dolan rezervasyonlari toplayan isin calisma araligi (saniye).
    /// </summary>
    [Range(5, 600)]
    public int ExpirySweepSeconds { get; set; } = 30;

    /// <summary>Bir turda islenecek en fazla rezervasyon.</summary>
    [Range(1, 1000)]
    public int ExpiryBatchSize { get; set; } = 100;
}

/// <summary>
/// <see cref="IReservationPolicy"/> arayuzunun yapilandirmadan beslenen hâli.
/// </summary>
/// <remarks>
/// Uygulama katmani <c>IOptions</c> tanimasin diye araya bu ince sinif
/// giriyor: handler'lar sade bir arayuz goruyor, yapilandirma bilgisi
/// altyapida kaliyor.
/// </remarks>
internal sealed class ReservationPolicy(ReservationOptions options) : IReservationPolicy
{
    public TimeSpan LockDuration { get; } = TimeSpan.FromMinutes(options.LockMinutes);

    public TimeSpan ExtendDuration { get; } = TimeSpan.FromMinutes(options.ExtendMinutes);

    public int MaxSeatsPerUserPerSession { get; } = options.MaxSeatsPerUserPerSession;
}
