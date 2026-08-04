using Loca.Application.Common.Models;

namespace Loca.Application.Features.Tickets.Common;

public static class TicketErrors
{
    public static readonly Error Unauthenticated =
        Error.Unauthorized("Ticket.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    /// <remarks>
    /// Baskasinin bileti soruldugunda da bu hata donuyor. "Sizin degil"
    /// denseydi cevap, var olan bir bilet kimligini dogrulamis olurdu.
    /// </remarks>
    public static readonly Error NotFound =
        Error.NotFound("Ticket.NotFound", "Bilet bulunamadi.");

    /// <remarks>
    /// Kapida okutulan kod hicbir bilete karsilik gelmiyor: sahte QR,
    /// baska bir sistemin kodu veya yanlis okuma. Biletin kendi
    /// durumundan kaynaklanan retler bu hatanin degil, basarili cevabin
    /// icinde doner.
    /// </remarks>
    public static readonly Error QrNotRecognised =
        Error.NotFound("Ticket.QrNotRecognised", "Bu kod bir bilete ait degil.");
}
