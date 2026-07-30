using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Bilet turu: Tam, Ogrenci, VIP gibi. Fiyat burada tanimlanir.
/// </summary>
/// <remarks>
/// Fiyat, koltuk uretimi sirasinda <c>EventSeats</c>'e <b>kopyalanir</b>,
/// referans verilmez. Referans verilseydi bilet turunun fiyati sonradan
/// degistiginde gecmiste satilmis biletlerin tutari da degisir ve muhasebe
/// tutmazdi.
/// </remarks>
public sealed class TicketType : BaseEntity
{
    private TicketType() => Name = string.Empty;

    public TicketType(
        Guid eventId,
        string name,
        Money price,
        int quota,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc,
        bool requiresVerification = false)
    {
        if (eventId == Guid.Empty)
            throw new DomainException("Bilet turu bir etkinlige bagli olmali.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bilet turu adi bos olamaz.");

        if (quota <= 0)
            throw new DomainException("Kontenjan sifirdan buyuk olmali.");

        if (salesEndsAtUtc <= salesStartsAtUtc)
            throw new DomainException("Satis bitisi satis baslangicindan sonra olmali.");

        EventId = eventId;
        Name = name.Trim();
        Price = price;
        Quota = quota;
        SalesStartsAtUtc = salesStartsAtUtc;
        SalesEndsAtUtc = salesEndsAtUtc;
        RequiresVerification = requiresVerification;
    }

    public Guid EventId { get; private set; }
    public Event? Event { get; private set; }

    /// <summary>
    /// Bu turun gecerli oldugu koltuk bolumu. <c>null</c> ise varsayilan tur.
    /// </summary>
    /// <remarks>
    /// Bolume atanmis bir tur yalnizca o bolumun koltuklarini fiyatlandirir
    /// (Balkon 200 TL, Orta 450 TL). Atanmamis tur, eslesmeyen tum bolumler
    /// icin varsayilan olarak kullanilir — koltuk uretiminde her koltugun bir
    /// fiyati olmak zorunda, aksi hâlde fiyatsiz koltuk olusur.
    /// </remarks>
    public Guid? SeatSectionId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Tutar ve para birimi birlikte. Money deger nesnesi negatife izin vermez.</summary>
    public Money Price { get; private set; }

    /// <summary>Bu turden satilabilecek en fazla bilet.</summary>
    public int Quota { get; private set; }

    public DateTime SalesStartsAtUtc { get; private set; }
    public DateTime SalesEndsAtUtc { get; private set; }

    /// <summary>Ogrenci bileti gibi, girişte belge dogrulamasi gerektirir.</summary>
    public bool RequiresVerification { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsOnSale(DateTime utcNow) =>
        IsActive && utcNow >= SalesStartsAtUtc && utcNow < SalesEndsAtUtc;

    /// <summary>
    /// Fiyati degistirir ve eski fiyati doner.
    /// </summary>
    /// <remarks>
    /// Eski deger doniyor cunku satisi baslamis bir bilet turunun fiyati
    /// degistiginde bu degisiklik <c>AuditLogs</c>'a "kim, ne zaman,
    /// eski → yeni" olarak yazilacak (yol haritasi Gun 5).
    /// </remarks>
    public Money ChangePrice(Money yeniFiyat)
    {
        var eski = Price;
        Price = yeniFiyat;
        return eski;
    }

    public void Update(string name, int quota, DateTime salesStartsAtUtc, DateTime salesEndsAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bilet turu adi bos olamaz.");

        if (quota <= 0)
            throw new DomainException("Kontenjan sifirdan buyuk olmali.");

        if (salesEndsAtUtc <= salesStartsAtUtc)
            throw new DomainException("Satis bitisi satis baslangicindan sonra olmali.");

        Name = name.Trim();
        Quota = quota;
        SalesStartsAtUtc = salesStartsAtUtc;
        SalesEndsAtUtc = salesEndsAtUtc;
    }

    /// <summary>
    /// Turu bir koltuk bolumune baglar; <c>null</c> gecilirse varsayilan
    /// tur hâline gelir.
    /// </summary>
    /// <remarks>
    /// Ayni bolumun iki aktif ture atanmamasi kurali burada degil
    /// <see cref="Event.AssignTicketTypeToSection"/> icinde: karar diger
    /// bilet turlerini gormeyi gerektiriyor, o liste aggregate root'ta.
    /// </remarks>
    public void AssignToSection(Guid? seatSectionId)
    {
        if (seatSectionId == Guid.Empty)
            throw new DomainException("Bolum kimligi bos olamaz.");

        SeatSectionId = seatSectionId;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
