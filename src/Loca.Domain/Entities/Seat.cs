using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Salonun FIZIKSEL koltugu. Etkinlikten bagimsiz ve kalicidir.
/// </summary>
/// <remarks>
/// <b>Projenin en kritik modelleme karari burada:</b> bu sinifta fiyat, satis
/// durumu veya kilit bilgisi <b>YOKTUR</b>. Bunlar <c>EventSeats</c>'te durur.
///
/// <para>
/// Sebep: ayni A-1 koltugu uc farkli oturumda satiliyorsa uc ayri duruma
/// sahiptir. Durum bu satirda tutulsaydi bir oturumda satilan koltuk
/// digerlerinde de satilmis gorunurdu.
/// </para>
///
/// <para>
/// Burada degisen tek sey <see cref="IsActive"/>: bozuk koltuk, kolon arkasi
/// veya teknik ekip icin ayrilmis koltuk pasiflestirilir ve hicbir oturumda
/// satisa cikmaz.
/// </para>
/// </remarks>
public sealed class Seat : BaseEntity
{
    private Seat() => RowLabel = string.Empty;

    public Seat(Guid seatSectionId, string rowLabel, int seatNumber, int positionX, int positionY)
    {
        if (seatSectionId == Guid.Empty)
            throw new DomainException("Koltuk bir bolume bagli olmali.");

        if (string.IsNullOrWhiteSpace(rowLabel))
            throw new DomainException("Sira etiketi bos olamaz.");

        if (seatNumber <= 0)
            throw new DomainException("Koltuk numarasi sifirdan buyuk olmali.");

        SeatSectionId = seatSectionId;
        RowLabel = rowLabel.Trim().ToUpperInvariant();
        SeatNumber = seatNumber;
        PositionX = positionX;
        PositionY = positionY;
    }

    public Guid SeatSectionId { get; private set; }
    public SeatSection? SeatSection { get; private set; }

    /// <summary>Sira etiketi: A, B, C... Buyuk harfe cevrilerek saklanir.</summary>
    public string RowLabel { get; private set; }

    public int SeatNumber { get; private set; }

    /// <summary>Gorsel plandaki yatay konum (SVG koordinati).</summary>
    public int PositionX { get; private set; }

    /// <summary>Gorsel plandaki dikey konum (SVG koordinati).</summary>
    public int PositionY { get; private set; }

    /// <summary>Pasif koltuk hicbir oturumda satisa cikmaz.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Kullaniciya gosterilen ad: "A-12".</summary>
    public string Label => $"{RowLabel}-{SeatNumber}";

    public void ToggleActive() => IsActive = !IsActive;

    public void MoveTo(int positionX, int positionY)
    {
        PositionX = positionX;
        PositionY = positionY;
    }
}
