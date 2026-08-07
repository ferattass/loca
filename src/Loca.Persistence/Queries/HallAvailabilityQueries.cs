using Loca.Application.Features.Venues.GetHallAvailability;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

/// <summary>
/// Salonun verilen aralikta hangi oturumlarla cakistigi.
/// </summary>
/// <remarks>
/// Kesisim mantigi <c>EventRepository.HallHasSessionConflictAsync</c> ile
/// AYNI: iki aralik [a1,a2] ve [b1,b2] su kosulda kesisir —
/// <c>a1 &lt; b2 &amp;&amp; a2 &gt; b1</c>, temizlik payi iki yana da eklenir.
/// Sik yapilan hata "yeni baslangic mevcut aralikta mi" seklindeki tek yonlu
/// kontroldur; o kontrol yeni araligin mevcut araligi TAMAMEN KAPSADIGI
/// durumu kacirir.
///
/// <para>
/// Iki yerde durmasinin sebebi donus tipleri: depo "var mi" diye
/// cevapliyor (<c>AnyAsync</c>, indeksten donuyor), burasi hangi oturum
/// oldugunu listeliyor. Kosul degisirse IKISI de degismeli — bu yorum o
/// baglantinin isareti.
/// </para>
/// </remarks>
internal sealed class HallAvailabilityQueries(LocaDbContext context) : IHallAvailabilityQueries
{
    public async Task<IReadOnlyList<DoluAralik>> GetConflictsAsync(
        Guid hallId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? excludeEventId,
        CancellationToken cancellationToken = default)
    {
        var pay = EventSession.TemizlikPayi;
        var baslangic = startsAtUtc - pay;
        var bitis = endsAtUtc + pay;

        return await context.EventSessions
            .Where(session =>
                session.HallId == hallId &&
                session.Status != EventSessionStatus.Cancelled &&
                (excludeEventId == null || session.EventId != excludeEventId) &&
                baslangic < session.EndsAtUtc &&
                bitis > session.StartsAtUtc)
            .OrderBy(session => session.StartsAtUtc)
            .Select(session => new DoluAralik(
                session.Id,
                session.Event!.Title,
                session.StartsAtUtc,
                session.EndsAtUtc))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
