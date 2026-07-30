using Loca.Application.Common.Models;
using Loca.Domain.Enums;

namespace Loca.Application.Features.Organizers.Common;

public sealed record OrganizerApplicationItem(
    Guid Id,
    Guid UserId,
    string UserFullName,
    string UserEmail,
    string CompanyName,
    string? TaxNumber,
    string ContactEmail,
    string ContactPhone,
    string? Website,
    Guid? DocumentFileId,
    OrganizerApplicationStatus Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? RejectionReason);

public static class OrganizerErrors
{
    public static readonly Error Unauthenticated =
        Error.Unauthorized("Organizer.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    public static readonly Error ApplicationNotFound =
        Error.NotFound("OrganizerApplication.NotFound", "Basvuru bulunamadi.");

    public static readonly Error PendingApplicationExists =
        Error.Conflict(
            "OrganizerApplication.PendingExists",
            "Zaten inceleme bekleyen bir basvurunuz var.");

    public static readonly Error AlreadyOrganizer =
        Error.Conflict("OrganizerApplication.AlreadyOrganizer", "Zaten organizatorsunuz.");

    public static readonly Error RoleMissing =
        Error.Conflict(
            "OrganizerApplication.RoleMissing",
            "Organizer rolu bulunamadi. Rol tohumlamasi calismamis olabilir.");
}
