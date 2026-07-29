using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Sehir. Etkinlik listelemesinde ana filtre.
/// </summary>
/// <remarks>
/// Serbest metin yerine tablo: "Istanbul", "istanbul" ve "İstanbul" ayri
/// degerler olarak birikirse sehre gore filtreleme calismaz.
/// Soft delete uygulanmaz — sehir silinmez, gerekirse pasiflestirilir.
/// </remarks>
public sealed class City : BaseEntity
{
    private City()
    {
        Name = string.Empty;
        PlateCode = string.Empty;
    }

    public City(string name, string plateCode)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Sehir adi bos olamaz.");

        Name = name.Trim();
        PlateCode = plateCode;
    }

    public string Name { get; private set; }

    /// <summary>Plaka kodu — siralama ve arama kolayligi icin.</summary>
    public string PlateCode { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
