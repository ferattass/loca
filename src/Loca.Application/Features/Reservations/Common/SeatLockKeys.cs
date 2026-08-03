using System.Globalization;

namespace Loca.Application.Features.Reservations.Common;

/// <summary>
/// Redis kilit anahtarlarinin tek uretim yeri.
/// </summary>
/// <remarks>
/// Bicim iki ayri yerde elle yazilsaydi (kilidi alan ve birakan kod) bir
/// harflik fark iki tarafin farkli anahtarlara bakmasina yol acardi: kilit
/// alinir ama birakilmaz, koltuk TTL boyunca bloke kalirdi. Bu tur bir hata
/// derlemede degil ancak yuk altinda gorunur.
///
/// <para>
/// Anahtar oturum ve koltuk kimligini birlikte tasiyor: ayni fiziksel
/// koltuk farkli oturumlarda ayri ayri kilitlenebilmeli.
/// </para>
/// </remarks>
public static class SeatLockKeys
{
    public static string For(Guid eventSessionId, Guid eventSeatId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"loca:lock:session:{eventSessionId:N}:seat:{eventSeatId:N}");

    public static IReadOnlyList<string> For(Guid eventSessionId, IEnumerable<Guid> eventSeatIds)
    {
        ArgumentNullException.ThrowIfNull(eventSeatIds);

        return eventSeatIds.Select(seatId => For(eventSessionId, seatId)).ToList();
    }
}
