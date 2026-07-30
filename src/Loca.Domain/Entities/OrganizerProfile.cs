using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Onaylanmis organizatorun sirket bilgileri.
/// </summary>
/// <remarks>
/// Kullanici ile bire bir. Bu alanlar <c>Users</c> tablosuna eklenebilirdi
/// ama o zaman her musteri satirinda bes bos kolon dolasirdi; ayrica
/// "organizator mu" sorusunun cevabi rol ile profilin varligi arasinda
/// bolunmus olurdu.
/// </remarks>
public sealed class OrganizerProfile : BaseEntity, IOwnedResource
{
    private OrganizerProfile()
    {
        CompanyName = string.Empty;
        ContactEmail = string.Empty;
        ContactPhone = string.Empty;
    }

    public OrganizerProfile(
        Guid userId,
        string companyName,
        string contactEmail,
        string contactPhone,
        string? taxNumber = null,
        string? website = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Profil bir kullaniciya bagli olmali.");

        if (string.IsNullOrWhiteSpace(companyName))
            throw new DomainException("Sirket adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new DomainException("Iletisim e-postasi bos olamaz.");

        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new DomainException("Iletisim telefonu bos olamaz.");

        UserId = userId;
        CompanyName = companyName.Trim();
        ContactEmail = contactEmail.Trim().ToLowerInvariant();
        ContactPhone = contactPhone.Trim();
        TaxNumber = taxNumber?.Trim();
        Website = website?.Trim();
    }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public string CompanyName { get; private set; }
    public string? TaxNumber { get; private set; }
    public string ContactEmail { get; private set; }
    public string ContactPhone { get; private set; }
    public string? Website { get; private set; }

    /// <summary>Admin tarafindan dogrulandi mi. Rozet gosteriminde kullanilir.</summary>
    public bool IsVerified { get; private set; }

    Guid IOwnedResource.OwnerId => UserId;

    public void Update(
        string companyName,
        string contactEmail,
        string contactPhone,
        string? taxNumber,
        string? website)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new DomainException("Sirket adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new DomainException("Iletisim e-postasi bos olamaz.");

        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new DomainException("Iletisim telefonu bos olamaz.");

        CompanyName = companyName.Trim();
        ContactEmail = contactEmail.Trim().ToLowerInvariant();
        ContactPhone = contactPhone.Trim();
        TaxNumber = taxNumber?.Trim();
        Website = website?.Trim();
    }

    public void MarkVerified() => IsVerified = true;

    public void RevokeVerification() => IsVerified = false;
}
