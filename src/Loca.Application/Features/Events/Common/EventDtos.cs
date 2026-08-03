using Loca.Domain.Enums;

namespace Loca.Application.Features.Events.Common;

/// <summary>
/// Liste satiri. Detay alanlari (aciklama, iptal politikasi) burada YOK:
/// yirmi kayitlik bir sayfada tasinmasi gereksiz veri olurdu.
/// </summary>
public sealed record EventListItem(
    Guid Id,
    string Title,
    string CategoryName,
    string CityName,
    string VenueName,
    DateTime EventDateUtc,
    int DurationMinutes,
    EventStatus Status,
    Guid? PosterFileId,
    int? MinimumAge,
    decimal? MinPrice,
    string? Currency,
    int SessionCount);

/// <summary>Etkinlik detayi. Tek sorguda projeksiyonla dolduruluyor.</summary>
public sealed record EventDetail(
    Guid Id,
    string Title,
    string Description,
    string CancellationPolicy,
    Guid CategoryId,
    string CategoryName,
    Guid OrganizerId,
    string OrganizerName,
    Guid CityId,
    string CityName,
    Guid VenueId,
    string VenueName,
    Guid HallId,
    string HallName,
    DateTime EventDateUtc,
    int DurationMinutes,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    EventStatus Status,
    Guid? PosterFileId,
    int? MinimumAge,
    DateTime? PublishedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<EventSessionItem> Sessions,
    IReadOnlyList<TicketTypeItem> TicketTypes);

public sealed record EventSessionItem(
    Guid Id,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    Guid HallId,
    string HallName,
    Guid SeatLayoutId,
    string SeatLayoutName,
    EventSessionStatus Status,
    bool SeatsGenerated);

public sealed record TicketTypeItem(
    Guid Id,
    string Name,
    decimal Price,
    string Currency,
    int Quota,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    bool RequiresVerification,
    Guid? SeatSectionId,
    string? SeatSectionName,
    bool IsActive);

/// <summary>Kategori listesi — etkinlik olusturma formunun ilk alani.</summary>
public sealed record EventCategoryItem(Guid Id, string Name, string Slug);

/// <summary>
/// Bir oturumun koltuk durumu, bolumlere gruplu.
/// </summary>
/// <remarks>
/// Duz koltuk listesi de dondurulebilirdi ama arayuz koltuklari bolum bolum
/// ciziyor; gruplamayi istemciye biraktirmak 600 elemanli bir diziyi her
/// yenilemede yeniden gruplamak demek olurdu.
/// </remarks>
/// <remarks>
/// Etkinlik adi, mekan ve baslangic saati de burada tasiniyor. Onceden
/// yalnizca koltuklar donuyordu; koltuk secim ekrani hangi etkinlik icin
/// koltuk sectigini yazamiyordu ve kullanici yanlis oturuma bakip bilet
/// alabilirdi. Ayri bir istek atmak yerine ayni yanitta getiriliyor:
/// veriler zaten ayni sorgunun join'inde.
/// </remarks>
public sealed record SeatAvailability(
    Guid EventSessionId,
    Guid SeatLayoutId,
    Guid EventId,
    string EventTitle,
    string VenueName,
    string HallName,
    DateTime StartsAtUtc,
    DateTime SalesEndsAtUtc,
    DateTime GeneratedAtUtc,
    IReadOnlyList<SeatAvailabilitySection> Sections);

/// <remarks>
/// Kilavuzun veri modelinde bolumun bir <c>ColorHex</c> alani da var; Gun
/// 4'te eklenmedigi icin burada yok. Sartnamenin zorunlu maddesi degil,
/// plan uzerindeki renk kodlamasi icin sonradan eklenecek.
/// </remarks>
public sealed record SeatAvailabilitySection(
    Guid Id,
    string Name,
    int DisplayOrder,
    IReadOnlyList<SeatAvailabilitySeat> Seats);

/// <remarks>
/// <c>LockedByUserId</c> BILINCLI OLARAK YOK. Koltugu kimin kilitledigi
/// baska bir kullanicinin kimligidir; disari verilmez. Istemcinin ihtiyaci
/// olan tek bilgi "bu kilit benim mi" — o da sunucuda hesaplanip
/// <see cref="IsLockedByMe"/> olarak donuyor.
/// </remarks>
public sealed record SeatAvailabilitySeat(
    Guid EventSeatId,
    Guid SeatId,
    string RowLabel,
    int SeatNumber,
    int PositionX,
    int PositionY,
    EventSeatStatus Status,
    DateTime? LockedUntilUtc,
    bool IsLockedByMe,
    decimal Price,
    string Currency,
    string TicketTypeName);
