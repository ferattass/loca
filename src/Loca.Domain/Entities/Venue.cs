using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Etkinliklerin gerceklestigi fiziksel mekan. Bir mekanda birden fazla salon olabilir.
/// </summary>
/// <remarks>
/// Soft delete uygulanir: gecmis satis kayitlari etkinlik uzerinden bu satira
/// baglidir. Fiziksel silme yabanci anahtar zincirini kirar ve raporlari bozar.
/// Ayrica etkinligi olan mekan silinemez (Venue → Events iliskisi Restrict).
/// </remarks>
public sealed class Venue : BaseEntity, IAggregateRoot, ISoftDeletable
{
    private readonly List<Hall> _halls = [];

    private Venue()
    {
        Name = string.Empty;
        Address = string.Empty;
    }

    public Venue(Guid cityId, string name, string address, string? description = null)
    {
        if (cityId == Guid.Empty)
            throw new DomainException("Mekan bir sehre bagli olmali.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Mekan adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException("Mekan adresi bos olamaz.");

        CityId = cityId;
        Name = name.Trim();
        Address = address.Trim();
        Description = description?.Trim();
    }

    public Guid CityId { get; private set; }
    public City? City { get; private set; }

    public string Name { get; private set; }
    public string Address { get; private set; }
    public string? Description { get; private set; }
    public string? PhoneNumber { get; private set; }

    /// <summary>Kapak gorseli. <c>UploadedFiles</c> tablosuna referans.</summary>
    public Guid? ImageFileId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyCollection<Hall> Halls => _halls;

    public void Update(string name, string address, string? description, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Mekan adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException("Mekan adresi bos olamaz.");

        Name = name.Trim();
        Address = address.Trim();
        Description = description?.Trim();
        PhoneNumber = phoneNumber?.Trim();
    }

    public void SetImage(Guid? imageFileId) => ImageFileId = imageFileId;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
