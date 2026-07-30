using Loca.Application.Common.Models;
using Loca.Domain.Enums;

namespace Loca.Application.Features.StudentVerifications.Common;

/// <param name="Identifier">
/// Ogrenciyi tanimlayan deger — kimlik numarasi varsa o, yoksa ogrenci
/// numarasi.
/// </param>
/// <param name="IdentifiedByStudentNumber">
/// <c>true</c> ise kimlik numarasi yok ve ogrenci numarasi esas alinmis.
/// Arayuz bunu gostererek kullaniciya hangi degerin kullanildigini bildirir.
/// </param>
public sealed record StudentVerificationDetail(
    Guid Id,
    Guid UserId,
    string FullName,
    string InstitutionName,
    string StudentNumber,
    string? NationalIdentityNumber,
    string Identifier,
    bool IdentifiedByStudentNumber,
    Guid? DocumentFileId,
    DateTime ValidUntilUtc,
    StudentVerificationStatus Status,
    DateTime? ReviewedAt,
    string? RejectionReason);

public static class StudentVerificationErrors
{
    public static readonly Error Unauthenticated =
        Error.Unauthorized("StudentVerification.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    public static readonly Error NotFound =
        Error.NotFound("StudentVerification.NotFound", "Ogrenci dogrulama kaydi bulunamadi.");

    /// <remarks>
    /// Mesaj OGRENCI NUMARASINI soyluyor, kimlik numarasini degil: tekillik
    /// (okul, ogrenci no) ikilisi uzerinden kurulu.
    /// </remarks>
    public static readonly Error StudentNumberTaken =
        Error.Conflict(
            "StudentVerification.StudentNumberTaken",
            "Bu okul ve ogrenci numarasi baska bir hesaba kayitli.");

    public static readonly Error NationalIdentityTaken =
        Error.Conflict(
            "StudentVerification.NationalIdentityTaken",
            "Bu kimlik numarasi baska bir hesaba kayitli.");

    public static readonly Error AlreadyApproved =
        Error.Conflict(
            "StudentVerification.AlreadyApproved",
            "Onaylanmis dogrulama kaydi degistirilemez.");
}
