namespace Loca.WebApi.Contracts.Venues;

public sealed record CreateVenueRequest(
    Guid CityId,
    string Name,
    string Address,
    string? Description,
    string? PhoneNumber);

public sealed record UpdateVenueRequest(
    string Name,
    string Address,
    string? Description,
    string? PhoneNumber);

public sealed record CreateHallRequest(string Name, int Capacity);

public sealed record UpdateHallRequest(string Name, int Capacity);

public sealed record CreateSeatLayoutRequest(Guid HallId, string Name, string? Description);

public sealed record CreateSeatSectionRequest(string Name, int DisplayOrder);

/// <param name="RowLabels">Sira etiketleri: ["A","B","C"].</param>
/// <param name="OriginY">Bolumun gorsel planda basladigi dikey konum.</param>
public sealed record GenerateSeatsRequest(
    Guid SeatSectionId,
    IReadOnlyList<string> RowLabels,
    int SeatsPerRow,
    int HorizontalSpacing = 30,
    int VerticalSpacing = 35,
    int OriginY = 0);
