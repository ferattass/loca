using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Oturma planindaki bolum: Orta Blok, Sag Balkon, Loca gibi.
/// </summary>
/// <remarks>
/// Bolum hem gorsel gruplama hem de fiyatlandirma birimi. Ayni etkinlikte
/// balkon ile parterin fiyati farkli olabilir; bu ayrim bolum uzerinden kurulur.
/// </remarks>
public sealed class SeatSection : BaseEntity
{
    private readonly List<Seat> _seats = [];

    private SeatSection() => Name = string.Empty;

    public SeatSection(Guid seatLayoutId, string name, int displayOrder = 0)
    {
        if (seatLayoutId == Guid.Empty)
            throw new DomainException("Bolum bir oturma planina bagli olmali.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bolum adi bos olamaz.");

        SeatLayoutId = seatLayoutId;
        Name = name.Trim();
        DisplayOrder = displayOrder;
    }

    public Guid SeatLayoutId { get; private set; }
    public SeatLayout? SeatLayout { get; private set; }

    public string Name { get; private set; }

    /// <summary>Gorsel planda soldan saga / onden arkaya siralama.</summary>
    public int DisplayOrder { get; private set; }

    public IReadOnlyCollection<Seat> Seats => _seats;

    public void Update(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bolum adi bos olamaz.");

        Name = name.Trim();
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Toplu koltuk uretimi. Uretilen koltuklar dondurulur; kaydetme
    /// sorumlulugu cagirana aittir (tek <c>AddRange</c>, tek <c>SaveChanges</c>).
    /// </summary>
    /// <param name="rowLabels">Sira etiketleri, ornegin A, B, C.</param>
    /// <param name="seatsPerRow">Her siradaki koltuk sayisi.</param>
    /// <param name="horizontalSpacing">Gorsel planda koltuklar arasi yatay mesafe.</param>
    /// <param name="verticalSpacing">Siralar arasi dikey mesafe.</param>
    /// <param name="originY">Bu bolumun gorsel planda basladigi dikey konum.</param>
    public IReadOnlyList<Seat> GenerateSeats(
        IReadOnlyList<string> rowLabels,
        int seatsPerRow,
        int horizontalSpacing = 30,
        int verticalSpacing = 35,
        int originY = 0)
    {
        ArgumentNullException.ThrowIfNull(rowLabels);

        if (rowLabels.Count == 0)
            throw new DomainException("En az bir sira etiketi gerekli.");

        if (seatsPerRow <= 0)
            throw new DomainException("Sira basina koltuk sayisi sifirdan buyuk olmali.");

        var generated = new List<Seat>(rowLabels.Count * seatsPerRow);

        for (var rowIndex = 0; rowIndex < rowLabels.Count; rowIndex++)
        {
            for (var seatIndex = 0; seatIndex < seatsPerRow; seatIndex++)
            {
                generated.Add(new Seat(
                    Id,
                    rowLabels[rowIndex],
                    seatIndex + 1,
                    positionX: seatIndex * horizontalSpacing,
                    positionY: originY + (rowIndex * verticalSpacing)));
            }
        }

        return generated;
    }
}
