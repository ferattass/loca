using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Organizator olma basvurusu. Admin onaylar veya reddeder.
/// </summary>
/// <remarks>
/// Sartnamedeki admin yetkisi: "organizator basvurularini onaylayabilir."
/// Kullanici kendi kendine Organizer rolu alamaz; alabilseydi herkes
/// etkinlik yayinlayabilir ve moderasyon zinciri anlamsizlasirdi.
/// </remarks>
public sealed class OrganizerApplication : BaseEntity, IOwnedResource
{
    private OrganizerApplication()
    {
        CompanyName = string.Empty;
        ContactEmail = string.Empty;
        ContactPhone = string.Empty;
    }

    public OrganizerApplication(
        Guid userId,
        string companyName,
        string contactEmail,
        string contactPhone,
        string? taxNumber = null,
        string? website = null,
        Guid? documentFileId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Basvuru bir kullaniciya bagli olmali.");

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
        DocumentFileId = documentFileId;
    }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public string CompanyName { get; private set; }
    public string? TaxNumber { get; private set; }
    public string ContactEmail { get; private set; }
    public string ContactPhone { get; private set; }
    public string? Website { get; private set; }

    /// <summary>Vergi levhasi veya faaliyet belgesi.</summary>
    public Guid? DocumentFileId { get; private set; }

    public OrganizerApplicationStatus Status { get; private set; } = OrganizerApplicationStatus.Pending;

    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    Guid IOwnedResource.OwnerId => UserId;

    /// <summary>
    /// Onaylar ve profil nesnesini uretir.
    /// </summary>
    /// <remarks>
    /// Profili basvurunun kendisi uretiyor: "onaylandi ama profil olusmadi"
    /// veya "profil var ama basvuru hâlâ beklemede" gibi tutarsiz ikililer
    /// ancak bu iki isi ayirmakla ortaya cikar.
    /// </remarks>
    public OrganizerProfile Approve(Guid reviewedBy, DateTime utcNow)
    {
        if (Status != OrganizerApplicationStatus.Pending)
            throw new DomainException($"Yalnizca bekleyen basvuru onaylanabilir. Mevcut durum: {Status}.");

        Status = OrganizerApplicationStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = utcNow;
        RejectionReason = null;

        var profile = new OrganizerProfile(
            UserId, CompanyName, ContactEmail, ContactPhone, TaxNumber, Website);

        profile.MarkVerified();

        return profile;
    }

    public void Reject(Guid reviewedBy, DateTime utcNow, string reason)
    {
        if (Status != OrganizerApplicationStatus.Pending)
            throw new DomainException($"Yalnizca bekleyen basvuru reddedilebilir. Mevcut durum: {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Red gerekcesi zorunludur.");

        Status = OrganizerApplicationStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = utcNow;
        RejectionReason = reason.Trim();
    }
}
