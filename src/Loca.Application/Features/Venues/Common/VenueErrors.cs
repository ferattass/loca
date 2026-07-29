using Loca.Application.Common.Models;

namespace Loca.Application.Features.Venues.Common;

internal static class VenueErrors
{
    internal static readonly Error NotFound =
        Error.NotFound("Venue.NotFound", "Mekan bulunamadi.");

    internal static readonly Error CityNotFound =
        Error.NotFound("Venue.CityNotFound", "Secilen sehir bulunamadi.");

    internal static readonly Error NameAlreadyExists =
        Error.Conflict("Venue.NameAlreadyExists", "Bu sehirde ayni adla baska bir mekan var.");

    /// <remarks>
    /// Salonlar mekana bagli; mekan silinince salonlarin sahipsiz kalmasi
    /// yerine once salonlarin kaldirilmasi isteniyor. Boylece silme islemi
    /// zincirleme bir kayba donusmuyor.
    /// </remarks>
    internal static readonly Error HasHalls =
        Error.Conflict("Venue.HasHalls", "Mekana bagli salonlar var. Once salonlari kaldirin.");
}
