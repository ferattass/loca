using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Etkinligin tek bir seansi: belirli tarih, belirli salon, belirli plan.
/// </summary>
/// <remarks>
/// Koltuklarin satilabilir hâli (<c>EventSeats</c>) bu satira baglidir.
/// Ayni etkinligin uc seansi varsa ayni fiziksel koltuk uc ayri
/// <c>EventSeat</c> satirina karsilik gelir.
/// </remarks>
public sealed class EventSession : BaseEntity
{
    /// <summary>
    /// Iki seans arasinda birakilmasi gereken en az sure.
    /// </summary>
    /// <remarks>
    /// Salon bosaltma, temizlik ve yeni seyircinin yerlesmesi icin. Sifir
    /// olsaydi bir seans biterken digeri baslar ve ayni salonda iki kalabalik
    /// karsi karsiya gelirdi.
    /// </remarks>
    public static readonly TimeSpan TemizlikPayi = TimeSpan.FromHours(1);

    private EventSession() { }

    public EventSession(
        Guid eventId,
        Guid hallId,
        Guid seatLayoutId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc)
    {
        if (eventId == Guid.Empty)
            throw new DomainException("Oturum bir etkinlige bagli olmali.");

        if (hallId == Guid.Empty)
            throw new DomainException("Oturum bir salona bagli olmali.");

        if (seatLayoutId == Guid.Empty)
            throw new DomainException("Oturum bir oturma planina bagli olmali.");

        EnsureValidDates(startsAtUtc, endsAtUtc, salesStartsAtUtc, salesEndsAtUtc);

        EventId = eventId;
        HallId = hallId;
        SeatLayoutId = seatLayoutId;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        SalesStartsAtUtc = salesStartsAtUtc;
        SalesEndsAtUtc = salesEndsAtUtc;
    }

    public Guid EventId { get; private set; }
    public Event? Event { get; private set; }

    public Guid HallId { get; private set; }
    public Hall? Hall { get; private set; }

    public Guid SeatLayoutId { get; private set; }
    public SeatLayout? SeatLayout { get; private set; }

    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public DateTime SalesStartsAtUtc { get; private set; }
    public DateTime SalesEndsAtUtc { get; private set; }

    public EventSessionStatus Status { get; private set; } = EventSessionStatus.Scheduled;

    /// <summary>Koltuklar uretildi mi. Iki kez uretilmesini engeller.</summary>
    public bool SeatsGenerated { get; private set; }

    /// <summary>
    /// Temizlik payi dahil, salonun bu oturum icin mesgul oldugu araligin sonu.
    /// </summary>
    public DateTime OccupiedUntilUtc => EndsAtUtc.Add(TemizlikPayi);

    /// <summary>
    /// Bu oturum verilen aralikla cakisiyor mu.
    /// </summary>
    /// <remarks>
    /// Klasik aralik cakismasi kurali: <c>yeniBas &lt; mevcutBitis &amp;&amp;
    /// yeniBitis &gt; mevcutBas</c>. Sadece "yeni baslangic mevcut aralikta mi"
    /// diye bakilsaydi, mevcut araligi tamamen kapsayan bir yeni oturum
    /// cakismiyor gorunurdu.
    /// </remarks>
    public bool OverlapsWith(DateTime baslangic, DateTime bitis) =>
        baslangic < OccupiedUntilUtc && bitis.Add(TemizlikPayi) > StartsAtUtc;

    public void Reschedule(
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc)
    {
        if (Status is EventSessionStatus.Cancelled or EventSessionStatus.Completed)
            throw new DomainException("Iptal edilmis veya tamamlanmis oturum degistirilemez.");

        EnsureValidDates(startsAtUtc, endsAtUtc, salesStartsAtUtc, salesEndsAtUtc);

        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        SalesStartsAtUtc = salesStartsAtUtc;
        SalesEndsAtUtc = salesEndsAtUtc;
    }

    /// <summary>
    /// Oturma planini degistirir.
    /// </summary>
    /// <remarks>
    /// Koltuklar uretildikten sonra plan degistirilemez: uretilmis
    /// <c>EventSeats</c> satirlari eski plana ait koltuklara referans veriyor.
    /// Plan degisseydi satilmis biletin hangi koltuga ait oldugu kaybolurdu.
    /// </remarks>
    public void ChangeSeatLayout(Guid seatLayoutId)
    {
        if (SeatsGenerated)
            throw new DomainException("Koltuklari uretilmis oturumun oturma plani degistirilemez.");

        if (seatLayoutId == Guid.Empty)
            throw new DomainException("Oturum bir oturma planina bagli olmali.");

        SeatLayoutId = seatLayoutId;
    }

    public void MarkSeatsGenerated()
    {
        if (SeatsGenerated)
            throw new DomainException("Bu oturumun koltuklari zaten uretilmis.");

        SeatsGenerated = true;
    }

    public void OpenSales()
    {
        if (Status != EventSessionStatus.Scheduled)
            throw new DomainException($"Bu durumdaki oturumda satis acilamaz: {Status}.");

        Status = EventSessionStatus.SalesOpen;
    }

    public void CloseSales()
    {
        if (Status != EventSessionStatus.SalesOpen)
            throw new DomainException($"Bu durumdaki oturumda satis kapatilamaz: {Status}.");

        Status = EventSessionStatus.SalesClosed;
    }

    public void Complete() => Status = EventSessionStatus.Completed;

    public void Cancel() => Status = EventSessionStatus.Cancelled;

    private static void EnsureValidDates(
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc)
    {
        if (endsAtUtc <= startsAtUtc)
            throw new DomainException("Oturum bitisi baslangicindan sonra olmali.");

        if (salesEndsAtUtc <= salesStartsAtUtc)
            throw new DomainException("Satis bitisi satis baslangicindan sonra olmali.");

        // Etkinlik basladiktan sonra bilet satmanin karsiligi yok: seyirci
        // iceri girmis, koltuk dolmus olur.
        if (salesEndsAtUtc > startsAtUtc)
            throw new DomainException("Satis, etkinlik baslamadan once bitmeli.");
    }
}
