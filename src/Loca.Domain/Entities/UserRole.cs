namespace Loca.Domain.Entities;

/// <summary>
/// Kullanici–rol baglantisi. Bilesik anahtar <c>(UserId, RoleId)</c> oldugu
/// icin <c>BaseEntity</c>'den turemez; ayrica bir Id tasimasinin anlami yok.
/// </summary>
public sealed class UserRole
{
    // EF Core materyalizasyon icin.
    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    public User? User { get; private set; }
    public Role? Role { get; private set; }
}
