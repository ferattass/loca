using Loca.Application.Common.Models;

namespace Loca.Application.Features.SeatLayouts.Common;

public sealed record SeatLayoutListItem(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int SectionCount);

public sealed record SeatLayoutResponse(
    Guid Id,
    Guid HallId,
    string HallName,
    int HallCapacity,
    string Name,
    string? Description,
    bool IsActive,
    int TotalSeatCount,
    IReadOnlyList<SeatSectionResponse> Sections);

public sealed record SeatSectionResponse(
    Guid Id,
    string Name,
    int DisplayOrder,
    IReadOnlyList<SeatResponse> Seats);

/// <param name="Label">Kullaniciya gosterilen ad: "A-12".</param>
public sealed record SeatResponse(
    Guid Id,
    string RowLabel,
    int SeatNumber,
    string Label,
    int PositionX,
    int PositionY,
    bool IsActive);

/// <summary>Toplu uretim sonucu.</summary>
public sealed record GenerateSeatsResponse(
    Guid SeatLayoutId,
    Guid SeatSectionId,
    int GeneratedCount,
    int TotalSeatCount);

internal static class SeatLayoutErrors
{
    internal static readonly Error NotFound =
        Error.NotFound("SeatLayout.NotFound", "Oturma plani bulunamadi.");

    internal static readonly Error HallNotFound =
        Error.NotFound("SeatLayout.HallNotFound", "Salon bulunamadi.");

    internal static readonly Error SectionNotFound =
        Error.NotFound("SeatLayout.SectionNotFound", "Bolum bulunamadi.");

    internal static readonly Error SeatNotFound =
        Error.NotFound("SeatLayout.SeatNotFound", "Koltuk bulunamadi.");

    internal static readonly Error NameAlreadyExists =
        Error.Conflict("SeatLayout.NameAlreadyExists", "Bu salonda ayni adla baska bir plan var.");

    internal static readonly Error SectionNameAlreadyExists =
        Error.Conflict("SeatLayout.SectionNameAlreadyExists", "Bu planda ayni adla baska bir bolum var.");

    /// <remarks>
    /// Yol haritasi Gun 4: koltuk sayisi salon kapasitesini asamaz → 409.
    /// Yangin yonetmeligi acisindan salon kapasitesi asilabilir bir sayi degil.
    /// </remarks>
    internal static readonly Error CapacityExceeded =
        Error.Conflict(
            "SeatLayout.CapacityExceeded",
            "Uretilecek koltuk sayisi salon kapasitesini asiyor.");

    internal static readonly Error SectionAlreadyHasSeats =
        Error.Conflict(
            "SeatLayout.SectionAlreadyHasSeats",
            "Bolumde zaten koltuk var. Once mevcut koltuklari kaldirin.");
}
