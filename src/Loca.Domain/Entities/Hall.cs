using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Mekan icindeki salon. Oturma planlari bu salona baglanir.
/// </summary>
/// <remarks>
/// <see cref="Capacity"/> fiziksel ust sinirdir: bir oturma planinda uretilen
/// koltuk sayisi bunu asamaz. Kontrol koltuk uretimi sirasinda yapilir ve
/// asilirsa 409 doner — yangin yonetmeligi acisindan salon kapasitesi
/// asilabilir bir sayi degildir.
/// </remarks>
public sealed class Hall : BaseEntity, ISoftDeletable
{
    private readonly List<SeatLayout> _seatLayouts = [];

    private Hall() => Name = string.Empty;

    public Hall(Guid venueId, string name, int capacity)
    {
        if (venueId == Guid.Empty)
            throw new DomainException("Salon bir mekana bagli olmali.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Salon adi bos olamaz.");

        if (capacity <= 0)
            throw new DomainException("Salon kapasitesi sifirdan buyuk olmali.");

        VenueId = venueId;
        Name = name.Trim();
        Capacity = capacity;
    }

    public Guid VenueId { get; private set; }
    public Venue? Venue { get; private set; }

    public string Name { get; private set; }

    /// <summary>Salonun alabilecegi en fazla kisi sayisi.</summary>
    public int Capacity { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyCollection<SeatLayout> SeatLayouts => _seatLayouts;

    public void Update(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Salon adi bos olamaz.");

        if (capacity <= 0)
            throw new DomainException("Salon kapasitesi sifirdan buyuk olmali.");

        Name = name.Trim();
        Capacity = capacity;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
