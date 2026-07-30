using Loca.Application.Common.Models;

namespace Loca.Application.Features.Events.Common;

/// <summary>
/// Etkinlik akisinin beklenen hatalari.
/// </summary>
/// <remarks>
/// Tek yerde toplaniyor cunku ayni durum iki handler'da farkli kod veya
/// farkli metinle donerse istemci tarafinda guvenilir hata isleme yazilamaz.
/// Kod degeri (<c>Event.NotFound</c>) makine icin, metin kullanici icin.
/// </remarks>
public static class EventErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Event.NotFound", "Etkinlik bulunamadi.");

    public static readonly Error CategoryNotFound =
        Error.NotFound("Event.CategoryNotFound", "Kategori bulunamadi veya aktif degil.");

    /// <remarks>
    /// 403, 404 degil: organizator panelinde kendisine ait olmayan bir
    /// kaynaga istek attiginda "yetkin yok" yaniti "kayit yok" yanitindan
    /// anlasilir. Kabul olcutu de bunu soyluyor.
    /// </remarks>
    public static readonly Error NotOwner =
        Error.Forbidden("Event.NotOwner", "Bu etkinlik size ait degil.");

    public static readonly Error Unauthenticated =
        Error.Unauthorized("Event.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    public static readonly Error PlaceInvalid =
        Error.Validation(
            "Event.PlaceInvalid",
            "Sehir, mekan ve salon birbirine ait degil. Salonun bagli oldugu mekani ve sehri kontrol edin.");

    public static readonly Error SeatLayoutNotInHall =
        Error.Validation(
            "Event.SeatLayoutNotInHall",
            "Secilen oturma plani bu salona ait degil veya aktif degil.");

    public static readonly Error HallConflict =
        Error.Conflict(
            "Event.HallConflict",
            "Bu salon secilen zaman araliginda baska bir etkinlige atanmis. " +
            "Oturumlar arasinda en az bir saat temizlik payi birakilmali.");

    public static readonly Error SessionNotFound =
        Error.NotFound("EventSession.NotFound", "Oturum bulunamadi.");

    public static readonly Error TicketTypeNotFound =
        Error.NotFound("TicketType.NotFound", "Bilet turu bulunamadi.");

    public static readonly Error TicketTypeNotInEvent =
        Error.Validation("TicketType.NotInEvent", "Bilet turu bu etkinlige ait degil.");

    public static readonly Error QuotaExceedsHallCapacity =
        Error.Conflict(
            "TicketType.QuotaExceedsCapacity",
            "Bilet turlerinin toplam kontenjani salon kapasitesini asamaz.");

    public static readonly Error SectionNotInHall =
        Error.Validation(
            "TicketType.SectionNotInHall",
            "Secilen bolum bu etkinligin salonundaki bir oturma planina ait degil.");

    public static readonly Error TicketTypeHasSoldSeats =
        Error.Conflict(
            "TicketType.HasSoldSeats",
            "Satisi yapilmis koltuklari olan bilet turu silinemez.");

    public static readonly Error SeatsAlreadyGenerated =
        Error.Conflict(
            "EventSession.SeatsAlreadyGenerated",
            "Bu oturumun koltuklari zaten uretilmis.");

    public static readonly Error NoSeatsInLayout =
        Error.Conflict(
            "EventSession.NoSeatsInLayout",
            "Oturma planinda aktif koltuk yok. Once koltuklari uretin.");

    public static readonly Error NoDefaultTicketType =
        Error.Conflict(
            "Event.NoDefaultTicketType",
            "Bolume atanmamis en az bir aktif bilet turu gerekli: " +
            "eslesmeyen bolumlerin koltuklari fiyatsiz kalirdi.");

    public static readonly Error SeatsNotGenerated =
        Error.NotFound(
            "EventSession.SeatsNotGenerated",
            "Bu oturumun koltuklari henuz uretilmedi. Etkinlik yayina alindiginda uretilir.");
}
