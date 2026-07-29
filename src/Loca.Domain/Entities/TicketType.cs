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

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
