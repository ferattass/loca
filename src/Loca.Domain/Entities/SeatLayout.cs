using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Bir salonun oturma plani. Ayni salonun birden fazla plani olabilir
/// (tiyatro duzeni, konser duzeni, sahne onu kaldirilmis duzen).
/// </summary>
/// <remarks>
/// Sartname: "kullanilmis oturma plani fiziksel olarak silinmemelidir."
/// Bir etkinlik oturumu bu plana gore koltuk uretmisse, plan silindiginde
/// satilmis biletlerin hangi koltuga ait oldugu kaybolur. Bu yuzden soft delete.
/// </remarks>
public sealed class SeatLayout : BaseEntity, IAggregateRoot, ISoftDeletable
{
    private readonly List<SeatSection> _sections = [];

    private SeatLayout() => Name = string.Empty;

    public SeatLayout(Guid hallId, string name, string? description = null)
    {
        if (hallId == Guid.Empty)
            throw new DomainException("Oturma plani bir salona bagli olmali.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plan adi bos olamaz.");

        HallId = hallId;
        Name = name.Trim();
        Description = description?.Trim();
    }

    public Guid HallId { get; private set; }
    public Hall? Hall { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyCollection<SeatSection> Sections => _sections;

    /// <summary>Plandaki toplam koltuk sayisi. Bolumler yuklu degilse 0 doner.</summary>
    public int TotalSeatCount => _sections.Sum(section => section.Seats.Count);

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plan adi bos olamaz.");

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
