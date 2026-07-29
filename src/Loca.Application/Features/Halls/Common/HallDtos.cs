using Loca.Application.Common.Models;

namespace Loca.Application.Features.Halls.Common;

public sealed record HallResponse(
    Guid Id,
    Guid VenueId,
    string VenueName,
    string Name,
    int Capacity,
    bool IsActive);

internal static class HallErrors
{
    internal static readonly Error NotFound =
        Error.NotFound("Hall.NotFound", "Salon bulunamadi.");

    internal static readonly Error VenueNotFound =
        Error.NotFound("Hall.VenueNotFound", "Mekan bulunamadi.");

    internal static readonly Error NameAlreadyExists =
        Error.Conflict("Hall.NameAlreadyExists", "Bu mekanda ayni adla baska bir salon var.");

    internal static readonly Error HasSeatLayouts =
        Error.Conflict(
            "Hall.HasSeatLayouts",
            "Salona bagli oturma planlari var. Once planlari kaldirin.");

    /// <remarks>
    /// Kapasite uretilmis koltuk sayisinin altina cekilemez. Cekilebilseydi
    /// salonda kapasitesinden fazla satilabilir koltuk bulunurdu.
    /// </remarks>
    internal static readonly Error CapacityBelowExistingSeats =
        Error.Conflict(
            "Hall.CapacityBelowExistingSeats",
            "Kapasite, planlarda uretilmis koltuk sayisinin altina indirilemez.");
}
