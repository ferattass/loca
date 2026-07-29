using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Etkinlik kategorisi: Konser, Tiyatro, Stand-up gibi.
/// </summary>
/// <remarks>
/// Sehirde oldugu gibi burada da serbest metin yerine tablo kullaniliyor:
/// "Tiyatro", "tiyatro" ve "Tiyatro " ayri degerler olarak birikirse
/// kategoriye gore filtreleme calismaz.
/// </remarks>
public sealed class EventCategory : BaseEntity
{
    private EventCategory()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public EventCategory(string name, string slug, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Kategori adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Kategori kisa adi bos olamaz.");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim();
    }

    public string Name { get; private set; }

    /// <summary>Adres cubugunda kullanilan kisa ad: "stand-up".</summary>
    public string Slug { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
