using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Satilmis tek bir koltugun bileti.
/// </summary>
/// <remarks>
/// Bilet, odeme basarili olduktan sonra ve <b>ayni transaction icinde</b>
/// uretilir. Odemeden bagimsiz uretilseydi, arada olusan bir hata parasi
/// alinmis ama bileti olmayan bir kullanici birakirdi.
///
/// <para>
/// <b>Etkinlik, koltuk ve fiyat bilgileri kopyalanir.</b> Yalnizca
/// <c>ReservationItemId</c> tutulup gerisi ilişki uzerinden okunsaydi,
/// etkinligin adi veya koltugun bolumu sonradan degistiginde gecmiste
/// kesilmis bilet de degisirdi. Bilet bir belgedir; kesildigi andaki hâlini
/// korumasi gerekir.
/// </para>
/// </remarks>
public sealed class Ticket : BaseEntity
{
    private Ticket()
    {
        TicketNumber = string.Empty;
        QrCode = string.Empty;
        EventTitle = string.Empty;
        SeatLabel = string.Empty;
        TicketTypeName = string.Empty;
    }

    public Ticket(
        Guid reservationId,
        Guid reservationItemId,
        Guid userId,
        Guid eventId,
        Guid eventSessionId,
        Guid eventSeatId,
        Guid ticketTypeId,
        string ticketNumber,
        string qrCode,
        string eventTitle,
        string seatLabel,
        string ticketTypeName,
        DateTime eventStartsAtUtc,
        Money price,
        DateTime issuedAtUtc)
    {
        if (reservationId == Guid.Empty)
            throw new DomainException("Bilet bir rezervasyona bagli olmali.");

        if (reservationItemId == Guid.Empty)
            throw new DomainException("Bilet bir rezervasyon kalemine bagli olmali.");

        if (userId == Guid.Empty)
            throw new DomainException("Bilet bir kullaniciya bagli olmali.");

        if (eventSeatId == Guid.Empty)
            throw new DomainException("Bilet bir koltuga bagli olmali.");

        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new DomainException("Bilet numarasi bos olamaz.");

        if (string.IsNullOrWhiteSpace(qrCode))
            throw new DomainException("QR kodu bos olamaz.");

        ReservationId = reservationId;
        ReservationItemId = reservationItemId;
        UserId = userId;
        EventId = eventId;
        EventSessionId = eventSessionId;
        EventSeatId = eventSeatId;
        TicketTypeId = ticketTypeId;
        TicketNumber = ticketNumber.Trim();
        QrCode = qrCode.Trim();
        EventTitle = eventTitle;
        SeatLabel = seatLabel;
        TicketTypeName = ticketTypeName;
        EventStartsAtUtc = eventStartsAtUtc;
        Price = price;
        IssuedAtUtc = issuedAtUtc;
    }

    public Guid ReservationId { get; private set; }
    public Reservation? Reservation { get; private set; }

    /// <summary>
    /// Bileti ureten rezervasyon kalemi.
    /// </summary>
    /// <remarks>
    /// Tekil: bir kalem yalnizca bir bilet uretir. Ayni odeme bildirimi iki
    /// kez islenirse veritabani kisiti ikinci uretimi reddeder — uygulama
    /// katmanindaki idempotency kontrolunun son savunma hatti.
    /// </remarks>
    public Guid ReservationItemId { get; private set; }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public Guid EventId { get; private set; }
    public Guid EventSessionId { get; private set; }
    public Guid EventSeatId { get; private set; }
    public Guid TicketTypeId { get; private set; }

    /// <summary>Kullaniciya gosterilen bilet numarasi. Tekil.</summary>
    public string TicketNumber { get; private set; }

    /// <summary>Giriste okutulan kod. Tekil ve tahmin edilemez olmali.</summary>
    public string QrCode { get; private set; }

    // --- Kesildigi andaki hâli (kopyalanan alanlar) ----------------------

    public string EventTitle { get; private set; }

    /// <summary>"Orta A-12" gibi.</summary>
    public string SeatLabel { get; private set; }

    public string TicketTypeName { get; private set; }

    public DateTime EventStartsAtUtc { get; private set; }

    public Money Price { get; private set; }

    public TicketStatus Status { get; private set; } = TicketStatus.Valid;

    public DateTime IssuedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>
    /// Satir surumu; eszamanli okutmalari ayirmak icin.
    /// </summary>
    /// <remarks>
    /// Iki kapi cihazi ayni QR'i ayni anda okutursa ikisi de bileti
    /// <c>Valid</c> gorup <see cref="MarkUsed"/> cagirabilir. Durum kontrolu
    /// tek basina yetmiyor: kontrol ile kayit arasinda gecen surede digeri
    /// yazmis oluyor. Surum damgasi ikinci yazmayi veritabani seviyesinde
    /// reddettiriyor; biletin ekran goruntusuyle iki kisinin girmesini
    /// engelleyen sey nihayetinde bu.
    /// </remarks>
    public uint Version { get; private set; }

    /// <summary>Giriste okutuldu.</summary>
    /// <remarks>
    /// Ikinci okutma reddedilir: ayni QR ile iki kisinin girmesi, bileti
    /// ekran goruntusu olarak paylasmakla mumkun olurdu.
    /// </remarks>
    public void MarkUsed(DateTime utcNow)
    {
        if (Status == TicketStatus.Used)
            throw new DomainException("Bu bilet daha once kullanilmis.");

        if (Status != TicketStatus.Valid)
            throw new DomainException($"Bu durumdaki bilet kullanilamaz: {Status}.");

        Status = TicketStatus.Used;
        UsedAtUtc = utcNow;
    }

    /// <summary>Etkinlik iptali veya rezervasyon iptali.</summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status is TicketStatus.Cancelled or TicketStatus.Refunded)
            return;

        Status = TicketStatus.Cancelled;
        CancelledAtUtc = utcNow;
    }

    /// <summary>Bedeli iade edildi.</summary>
    public void MarkRefunded(DateTime utcNow)
    {
        if (Status == TicketStatus.Refunded)
            return;

        if (Status == TicketStatus.Used)
            throw new DomainException("Kullanilmis bilet iade edilemez.");

        Status = TicketStatus.Refunded;
        CancelledAtUtc ??= utcNow;
    }
}
