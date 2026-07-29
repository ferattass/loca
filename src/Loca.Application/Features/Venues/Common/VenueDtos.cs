namespace Loca.Application.Features.Venues.Common;

/// <summary>
/// Liste ekraninda gosterilen ozet. Adres ve aciklama tasinmaz —
/// listede gorunmuyor, tasinmasi gereksiz yuk.
/// </summary>
public sealed record VenueListItem(
    Guid Id,
    string Name,
    string CityName,
    bool IsActive,
    int HallCount);

public sealed record VenueResponse(
    Guid Id,
    Guid CityId,
    string CityName,
    string Name,
    string Address,
    string? Description,
    string? PhoneNumber,
    Guid? ImageFileId,
    bool IsActive,
    IReadOnlyList<VenueHallSummary> Halls);

public sealed record VenueHallSummary(Guid Id, string Name, int Capacity, bool IsActive);
