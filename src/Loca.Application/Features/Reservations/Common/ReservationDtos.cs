using Loca.Domain.Enums;

namespace Loca.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyonun tek bir koltugu.
/// </summary>
/// <remarks>
/// Sira ve koltuk numarasi burada da tasiniyor: kullanici "A-12" gormeli,
/// koltugun kimligini degil.
/// </remarks>
public sealed record ReservationSeatItem(
    Guid EventSeatId,
    Guid SeatId,
    string SectionName,
    string RowLabel,
    int SeatNumber,
    string TicketTypeName,
    decimal Price,
    string Currency);

/// <param name="RemainingSeconds">
/// Kilidin bitmesine kalan saniye.
/// </param>
/// <remarks>
/// <b>Geri sayim istemcide degil SUNUCUDA hesaplanir.</b> Istemci
/// <c>ExpiresAtUtc</c> ile kendi saatini karsilastirsaydi, saati geri
/// alinmis bir makinede sayac hic bitmez ve kullanici suresi coktan dolmus
/// bir rezervasyonu odemeye calisirdi. Istemci bu sayiyi baslangic degeri
/// alip yerel olarak azaltir.
/// </remarks>
public sealed record ReservationDetail(
    Guid Id,
    Guid EventSessionId,
    Guid EventId,
    string EventTitle,
    DateTime SessionStartsAtUtc,
    string VenueName,
    string HallName,
    ReservationStatus Status,
    DateTime ExpiresAtUtc,
    int RemainingSeconds,
    bool ExtensionUsed,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    IReadOnlyList<ReservationSeatItem> Seats);

/// <summary>"Rezervasyonlarim" listesi icin ozet satir.</summary>
public sealed record ReservationListItem(
    Guid Id,
    Guid EventSessionId,
    Guid EventId,
    string EventTitle,
    DateTime SessionStartsAtUtc,
    string VenueName,
    ReservationStatus Status,
    DateTime ExpiresAtUtc,
    int RemainingSeconds,
    int SeatCount,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);
