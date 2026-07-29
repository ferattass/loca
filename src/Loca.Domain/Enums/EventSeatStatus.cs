namespace Loca.Domain.Enums;

/// <summary>
/// Bir koltugun BELIRLI BIR OTURUMDAKI satis durumu.
/// </summary>
/// <remarks>
/// Bu durum <c>Seats</c> tablosunda degil <c>EventSeats</c>'te tutulur:
/// ayni fiziksel koltuk uc farkli oturumda uc ayri duruma sahiptir.
/// Durum fiziksel koltukta tutulsaydi bir oturumda satilan koltuk
/// digerlerinde de satilmis gorunurdu.
/// </remarks>
public enum EventSeatStatus
{
    /// <summary>Bos, secilebilir.</summary>
    Available = 1,

    /// <summary>Gecici olarak kilitli. <c>LockedUntil</c> dolunca serbest kalir.</summary>
    Locked = 2,

    /// <summary>Rezervasyona baglandi, odeme bekleniyor.</summary>
    Reserved = 3,

    /// <summary>Satildi. Bilet uretildi.</summary>
    Sold = 4,

    /// <summary>Bu oturumda satisa kapali (bozuk koltuk, teknik ekip alani).</summary>
    Disabled = 5
}

/// <summary>Bir oturumun durumu.</summary>
public enum EventSessionStatus
{
    Scheduled = 1,
    SalesOpen = 2,
    SalesClosed = 3,
    Completed = 4,
    Cancelled = 5
}
