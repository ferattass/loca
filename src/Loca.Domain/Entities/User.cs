using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Sisteme giris yapan kisi. Rolleri <c>UserRoles</c> uzerinden tasinir.
/// </summary>
/// <remarks>
/// Kullanici kaydi silinmez, pasiflestirilir: satis gecmisi ve muhasebe
/// kayitlari bu satira referans verir. Bu yuzden <c>ISoftDeletable</c> da
/// uygulanmaz — veri modeli belgesinde soft delete yalnizca Events, Venues,
/// Halls ve SeatLayouts icin ongoruldu.
/// </remarks>
public sealed class User : BaseEntity, IAggregateRoot
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    // EF Core materyalizasyon icin.
    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FullName = string.Empty;
    }

    public User(string email, string passwordHash, string fullName, string? phoneNumber = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("E-posta bos olamaz.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Sifre ozeti bos olamaz.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Ad soyad bos olamaz.");

        Email = Normalize(email);
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber?.Trim();
    }

    /// <remarks>
    /// Kucuk harfe cevrilmis ve kirpilmis halde saklanir. Aksi hâlde
    /// "Ferat@x.com" ile "ferat@x.com" iki ayri kayit olur ve UNIQUE index
    /// bunu engelleyemez.
    /// </remarks>
    public string Email { get; private set; }

    /// <summary>BCrypt ozeti. Duz sifre hicbir yerde saklanmaz ve loglanmaz.</summary>
    public string PasswordHash { get; private set; }

    public string FullName { get; private set; }
    public string? PhoneNumber { get; private set; }

    /// <summary>Pasif kullanici giris yapamaz. Silme yerine bu alan kullanilir.</summary>
    public bool IsActive { get; private set; } = true;

    public bool EmailConfirmed { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public bool HasRole(string roleName) =>
        _userRoles.Any(userRole =>
            string.Equals(userRole.Role?.Name, roleName, StringComparison.Ordinal));

    public void AssignRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        // Ayni rol iki kez atanirsa composite anahtar ihlali olusur;
        // hatayi veritabanina birakmak yerine burada sessizce yutulur.
        if (_userRoles.Exists(userRole => userRole.RoleId == role.Id))
            return;

        _userRoles.Add(new UserRole(Id, role.Id));
    }

    /// <summary>Rolu geri alir.</summary>
    /// <remarks>
    /// Rolu olmayan kullanicidan rol almak hata degil: cagiran taraf ayni
    /// sonuca ulasmis oluyor. Hata firlatilsaydi arayuzun once "bu rol
    /// var mi" diye sorup sonra silmesi gerekirdi ve iki cagri arasinda
    /// baskasi rolu almis olabilirdi.
    /// </remarks>
    public void RemoveRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        _userRoles.RemoveAll(userRole => userRole.RoleId == role.Id);
    }

    public void AddRefreshToken(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _refreshTokens.Add(refreshToken);
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("Sifre ozeti bos olamaz.");

        PasswordHash = newPasswordHash;
    }

    /// <remarks>
    /// Zaman disaridan verilir. Entity icinde <c>DateTime.UtcNow</c> cagrilsaydi
    /// testte "giris zamani dogru yazildi mi" sorusu dogrulanamazdi.
    /// </remarks>
    public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void ConfirmEmail() => EmailConfirmed = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
