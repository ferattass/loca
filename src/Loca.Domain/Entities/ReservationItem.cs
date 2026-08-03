using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Rezervasyonun tek bir koltugu.
/// </summary>
/// <remarks>
/// <b>Fiyat burada da KOPYALANIR.</b> <c>EventSeat.Price</c> zaten bilet
/// turunden kopyalanmis bir deger; buraya ikinci kez kopyalanmasinin sebebi
/// farkli: koltugun fiyati kural olarak degismese de bir gun degistirilirse
/// (yonetici duzeltmesi, kampanya) kullanicinin ONAYLADIGI tutar ile tahsil
/// edilecek tutar ayrisir. Rezervasyon toplami bu satirlarin toplamidir;
/// koltuk tablosuna bakilarak yeniden hesaplanmaz.
/// </remarks>
public sealed class ReservationItem : BaseEntity
{
    private ReservationItem() { }

    /// <remarks>
    /// <c>internal</c>: kalem yalnizca <see cref="Reservation"/> icinden
    /// olusturulur. Disaridan serbestce eklenebilseydi toplam tutar ile
    /// kalemlerin toplami ayrisabilirdi — toplami aggregate root koruyor.
    /// </remarks>
    internal ReservationItem(Guid reservationId, Guid eventSeatId, Guid ticketTypeId, Money price)
    {
        if (reservationId == Guid.Empty)
            throw new DomainException("Kalem bir rezervasyona bagli olmali.");

        if (eventSeatId == Guid.Empty)
            throw new DomainException("Kalem bir koltuga bagli olmali.");

        if (ticketTypeId == Guid.Empty)
            throw new DomainException("Kalem bir bilet turune bagli olmali.");

        ReservationId = reservationId;
        EventSeatId = eventSeatId;
        TicketTypeId = ticketTypeId;
        Price = price;
    }

    public Guid ReservationId { get; private set; }
    public Reservation? Reservation { get; private set; }

    public Guid EventSeatId { get; private set; }
    public EventSeat? EventSeat { get; private set; }

    public Guid TicketTypeId { get; private set; }
    public TicketType? TicketType { get; private set; }

    /// <summary>Rezervasyon anindaki birim fiyat.</summary>
    public Money Price { get; private set; }
}
