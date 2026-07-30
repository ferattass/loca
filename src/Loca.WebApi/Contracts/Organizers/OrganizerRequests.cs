namespace Loca.WebApi.Contracts.Organizers;

public sealed record ApplyForOrganizerRequest(
    string CompanyName,
    string ContactEmail,
    string ContactPhone,
    string? TaxNumber,
    string? Website,
    Guid? DocumentFileId);

public sealed record ReviewApplicationRequest(bool Approve, string? RejectionReason);

/// <param name="NationalIdentityNumber">
/// <b>Opsiyonel.</b> Bos birakilabilir; yabanci uyruklu ogrencide kimlik
/// numarasi bulunmaz. Verilmediginde ogrenci numarasi esas alinir.
/// </param>
public sealed record SubmitStudentVerificationRequest(
    string FullName,
    string InstitutionName,
    string StudentNumber,
    DateTime ValidUntilUtc,
    string? NationalIdentityNumber,
    Guid? DocumentFileId);

public sealed record ReviewStudentVerificationRequest(bool Approve, string? RejectionReason);
