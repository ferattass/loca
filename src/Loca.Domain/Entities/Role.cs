using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Sistemdeki rol tanimi. Uc satirla sabittir ve migration ile tohumlanir.
/// </summary>
/// <remarks>
/// Rol neden enum degil de tablo? Yetki matrisinde rollerin aciklamasi
/// yonetim ekraninda gosterilecek ve <c>UserRoles</c> uzerinden yabanci
/// anahtar kurulacak. Enum olsaydi veritabani bu butunlugu koruyamazdi.
/// </remarks>
public sealed class Role : BaseEntity
{
    // EF Core materyalizasyon icin.
    private Role()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public Role(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Rol adi bos olamaz.");

        Name = name;
        Description = description;
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
}
