using Loca.Domain.Common;
using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Bilet uretimi icin gereken, rezervasyon kaleminin cozulmus hâli.
/// </summary>
/// <remarks>
/// Bilet kesildigi andaki bilgileri KOPYALIYOR (etkinlik adi, koltuk
/// etiketi, tur adi); bu kayit o alanlari tek sorguda toplayip getiriyor.
/// Kalem basina ayri sorgu calistirilsaydi alti koltukluk bir rezervasyonda
/// alti gidis donus olurdu.
/// </remarks>
public sealed record TicketSource(
    Guid ReservationItemId,
    Guid EventSeatId,
    Guid TicketTypeId,
    Guid EventId,
    Guid EventSessionId,
    string EventTitle,
    string SectionName,
    string RowLabel,
    int SeatNumber,
    string TicketTypeName,
    DateTime EventStartsAtUtc,
    Money Price);

public interface IPaymentRepository
{
    Task<Payment?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rezervasyonun BASARILI odemesi varsa doner.
    /// </summary>
    /// <remarks>
    /// "Ayni rezervasyon icin birden fazla basarili odeme olusamaz" kuralinin
    /// uygulama tarafi. Veritabani tarafi kismi tekil index ile bagli.
    /// </remarks>
    Task<Payment?> GetSuccessfulByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Rezervasyonun sonuc bekleyen odemesi. Ikinci kez baslatmayi engeller.</summary>
    Task<Payment?> GetPendingByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdempotencyKeyAsync(
        Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Bilet uretimi icin rezervasyon kalemlerinin cozulmus hâli.</summary>
    Task<IReadOnlyList<TicketSource>> GetTicketSourcesAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
