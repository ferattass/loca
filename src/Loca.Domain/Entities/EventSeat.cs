using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Bir fiziksel koltugun BELIRLI BIR OTURUMDAKI satilabilir hâli.
/// </summary>
/// <remarks>
/// <b>Projenin en kritik modelleme karari.</b> <c>Seats</c> salonun kalici,
/// fiziksel koltugudur; fiyat, kilit ve satis durumu ORADA DEGIL burada durur.
/// Ayni A-1 koltugu uc oturumda uc ayri satirdir.
///
/// <para>
/// Eszamanlilik icin uc katmanli savunma kurgulanacak (Gun 6):
/// Redis TTL kilidi hizli on eleme, veritabani islemi + <see cref="Version"/>
/// gercek tutarlilik, <c>UNIQUE(EventSessionId, SeatId)</c> son savunma hatti.
/// Redis kilidini almak koltugun veritabaninda hâlâ bos oldugunu GARANTI
/// ETMEZ; kilit alindiktan sonra durum burada yeniden dogrulanir.
/// </para>
/// </remarks>
public sealed class EventSeat : BaseEntity
{
    private EventSeat() { }

    public EventSeat(Guid eventSessionId, Guid seatId, Guid ticketTypeId, Money price)
    {
        if (eventSessionId == Guid.Empty)
            throw new DomainException("Koltuk bir oturuma bagli olmali.");

        if (seatId == Guid.Empty)
            throw new DomainException("Fiziksel koltuk zorunlu.");

        if (ticketTypeId == Guid.Empty)
            throw new DomainException("Koltuk bir bilet turune bagli olmali.");

        EventSessionId = eventSessionId;
        SeatId = seatId;
        TicketTypeId = ticketTypeId;
        Price = price;
    }

    public Guid EventSessionId { get; private set; }
    public EventSession? EventSession { get; private set; }

    public Guid SeatId { get; private set; }
    public Seat? Seat { get; private set; }

    public Guid TicketTypeId { get; private set; }
    public TicketType? TicketType { get; private set; }

    /// <summary>
    /// Uretim aninda bilet turunden KOPYALANIR.
    /// </summary>
    /// <remarks>
    /// Referans verilseydi bilet turunun fiyati sonradan degistiginde
    /// gecmiste satilmis biletlerin tutari da degisirdi.
    /// </remarks>
    public Money Price { get; private set; }

    public EventSeatStatus Status { get; private set; } = EventSeatStatus.Available;

    public DateTime? LockedUntilUtc { get; private set; }
    public Guid? LockedByUserId { get; private set; }
    public Guid? ReservationId { get; private set; }

    /// <summary>
    /// Eszamanlilik damgasi (PostgreSQL <c>xmin</c>).
    /// </summary>
    /// <remarks>
    /// Iki istek ayni satiri okuyup guncellemeye calistiginda ikincisi
    /// <c>DbUpdateConcurrencyException</c> alir ve 409'a cevrilir. Bu olmadan
    /// son yazan kazanir ve ayni koltuk iki kisiye satilir.
    /// </remarks>
    public uint Version { get; private set; }

    /// <summary>Kilidin suresi dolmus mu.</summary>
    public bool IsLockExpired(DateTime utcNow) =>
        Status == EventSeatStatus.Locked && LockedUntilUtc is { } bitis && utcNow >= bitis;

    /// <summary>Su anda secilebilir mi. Suresi dolmus kilit bos sayilir.</summary>
    public bool IsAvailable(DateTime utcNow) =>
        Status == EventSeatStatus.Available || IsLockExpired(utcNow);

    /// <summary>
    /// Koltugu gecici olarak kilitler.
    /// </summary>
    /// <remarks>
    /// Suresi dolmus kilit uzerine yeni kilit konabilir; temizlik isini
    /// bekleyen bir koltuk baskasina satilamaz durumda kalmasin.
    /// </remarks>
    public void Lock(Guid userId, DateTime utcNow, TimeSpan sure)
    {
        if (!IsAvailable(utcNow))
            throw new DomainException("Koltuk su anda secilebilir durumda degil.");

        Status = EventSeatStatus.Locked;
        LockedByUserId = userId;
        LockedUntilUtc = utcNow.Add(sure);
    }

    /// <summary>Kilidi uzatir. Cagiran, uzatma hakkinin kullanilip kullanilmadigini kontrol eder.</summary>
    public void ExtendLock(DateTime utcNow, TimeSpan ek)
    {
        if (Status != EventSeatStatus.Locked)
            throw new DomainException("Yalnizca kilitli koltugun suresi uzatilabilir.");

        if (IsLockExpired(utcNow))
            throw new DomainException("Suresi dolmus kilit uzatilamaz.");

        LockedUntilUtc = (LockedUntilUtc ?? utcNow).Add(ek);
    }

    /// <summary>Rezervasyona baglar: Locked → Reserved.</summary>
    public void AttachToReservation(Guid reservationId)
    {
        if (Status != EventSeatStatus.Locked)
            throw new DomainException("Yalnizca kilitli koltuk rezervasyona baglanabilir.");

        Status = EventSeatStatus.Reserved;
        ReservationId = reservationId;
    }

    /// <summary>Odeme tamamlandi: Reserved → Sold.</summary>
    public void MarkSold()
    {
        if (Status != EventSeatStatus.Reserved)
            throw new DomainException("Yalnizca rezerve koltuk satilmis olarak isaretlenebilir.");

        Status = EventSeatStatus.Sold;
        LockedUntilUtc = null;
        LockedByUserId = null;
    }

    /// <summary>
    /// Koltugu serbest birakir: kilit dustu, rezervasyon iptal oldu veya
    /// odeme basarisiz.
    /// </summary>
    public void Release()
    {
        if (Status == EventSeatStatus.Sold)
            throw new DomainException("Satilmis koltuk serbest birakilamaz.");

        Status = EventSeatStatus.Available;
        LockedUntilUtc = null;
        LockedByUserId = null;
        ReservationId = null;
    }

    /// <summary>Bu oturum icin satisa kapatir.</summary>
    public void Disable()
    {
        if (Status is EventSeatStatus.Sold or EventSeatStatus.Reserved)
            throw new DomainException("Satilmis veya rezerve koltuk devre disi birakilamaz.");

        Status = EventSeatStatus.Disabled;
    }
}
