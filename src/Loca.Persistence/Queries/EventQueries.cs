using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Enums;
using Loca.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

/// <summary>
/// Etkinlik okuma sorgulari. Hepsi projeksiyonla tek <c>SELECT</c>'e iner.
/// </summary>
internal sealed class EventQueries(LocaDbContext context) : IEventQueries
{
    /// <summary>
    /// Herkese acik sayilan durumlar.
    /// </summary>
    /// <remarks>
    /// Taslak ve onay bekleyen etkinlikler burada YOK: organizatorun henuz
    /// duyurmadigi isi anonim listede gorunurse hem surpriz olur hem de
    /// rakip organizatore bilgi sizar. Iptal edilen de yok; ana listede
    /// gosterilmesinin karsiligi olmaz.
    /// </remarks>
    private static readonly EventStatus[] PublicStatuses =
    [
        EventStatus.Published,
        EventStatus.SalesOpen,
        EventStatus.SalesClosed,
        EventStatus.Completed
    ];

    public async Task<PagedResult<EventListItem>> GetPagedAsync(
        EventListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var sorgu = context.Events.AsNoTracking();

        if (filter.OrganizerId is { } organizator)
            sorgu = sorgu.Where(ev => ev.OrganizerId == organizator);

        if (filter.OnlyPublic)
            sorgu = sorgu.Where(ev => PublicStatuses.Contains(ev.Status));

        if (filter.Status is { } durum)
            sorgu = sorgu.Where(ev => ev.Status == durum);

        if (filter.CategoryId is { } kategori)
            sorgu = sorgu.Where(ev => ev.CategoryId == kategori);

        // Sehir Event uzerinde ayri bir kolon (EventPlace deger nesnesi duz
        // kolonlara aciliyor); mekan uzerinden JOIN gerekmiyor ve
        // (CityId, CategoryId, EventDateUtc) bilesik index'i kullaniliyor.
        if (filter.CityId is { } sehir)
            sorgu = sorgu.Where(ev => ev.CityId == sehir);

        // Tarih araligi EventDateUtc uzerinden: bu alan en erken oturumun
        // baslangicina hizali tutuluyor (Event.AlignEventDateWithSessions),
        // yani kullanicinin listede gordugu tarihle ayni. Oturum tablosuna
        // gidilseydi ayni etkinlik birden fazla kez donerdi.
        if (filter.FromUtc is { } baslangic)
            sorgu = sorgu.Where(ev => ev.EventDateUtc >= baslangic);

        if (filter.ToUtc is { } bitis)
            sorgu = sorgu.Where(ev => ev.EventDateUtc < bitis);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // ILIKE, ToLower degil: ToLower kolon uzerinde fonksiyon
            // cagirdigi icin index kullanilamaz hâle geliyor ve calisan
            // makinenin kulturune bagli (Turkce'de buyuk I noktasiz i'ye
            // donuyor). Kalip karakterleri (%, _, \) kaciriliyor — adinda
            // % gecen bir etkinlik aranirken kalip "her sey" anlamina
            // gelirdi.
            var kalip = LikePattern.Contains(filter.Search);

            sorgu = sorgu.Where(ev => EF.Functions.ILike(ev.Title, kalip));
        }

        // Sayim sayfalamadan ONCE: toplam kayit sayisi sayfa buyuklugunden
        // bagimsizdir.
        var toplam = await sorgu.CountAsync(cancellationToken);

        var kayitlar = await sorgu
            .OrderBy(ev => ev.EventDateUtc)
            .ThenBy(ev => ev.Title)
            .Skip(filter.Pagination.Skip)
            .Take(filter.Pagination.PageSize)
            // Include DEGIL Select: Include tum kolonlari getirir ve
            // kategori/sehir/mekan satirlarini change tracker'a yazar.
            // Burada gereken yalnizca uc ad.
            .Select(ev => new EventListItem(
                ev.Id,
                ev.Title,
                ev.Category!.Name,
                ev.City!.Name,
                ev.Venue!.Name,
                ev.EventDateUtc,
                ev.DurationMinutes,
                ev.Status,
                ev.PosterFileId,
                ev.MinimumAge,
                // En dusuk fiyat "1.250 TL'den baslayan fiyatlarla" ifadesi
                // icin. Alt sorgu olarak ceviriliyor, ek gidis donus yok.
                ev.TicketTypes
                    .Where(ticketType => ticketType.IsActive)
                    .Min(ticketType => (decimal?)ticketType.Price.Amount),
                ev.TicketTypes
                    .Where(ticketType => ticketType.IsActive)
                    .Select(ticketType => ticketType.Price.Currency)
                    .FirstOrDefault(),
                ev.Sessions.Count(session => session.Status != EventSessionStatus.Cancelled)))
            .ToListAsync(cancellationToken);

        return new PagedResult<EventListItem>
        {
            Items = kayitlar,
            PageNumber = filter.Pagination.PageNumber,
            PageSize = filter.Pagination.PageSize,
            TotalCount = toplam
        };
    }

    public async Task<EventDetail?> GetDetailAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await context.Events
            .Where(ev => ev.Id == id)
            .Select(ev => new EventDetail(
                ev.Id,
                ev.Title,
                ev.Description,
                ev.CancellationPolicy,
                ev.CategoryId,
                ev.Category!.Name,
                ev.OrganizerId,
                ev.Organizer!.FullName,
                ev.CityId,
                ev.City!.Name,
                ev.VenueId,
                ev.Venue!.Name,
                ev.HallId,
                ev.Hall!.Name,
                ev.EventDateUtc,
                ev.DurationMinutes,
                ev.SalesStartsAtUtc,
                ev.SalesEndsAtUtc,
                ev.Status,
                ev.PosterFileId,
                ev.MinimumAge,
                ev.PublishedAt,
                ev.CancelledAt,
                ev.CancellationReason,
                ev.Sessions
                    .OrderBy(session => session.StartsAtUtc)
                    .Select(session => new EventSessionItem(
                        session.Id,
                        session.StartsAtUtc,
                        session.EndsAtUtc,
                        session.SalesStartsAtUtc,
                        session.SalesEndsAtUtc,
                        session.HallId,
                        session.Hall!.Name,
                        session.SeatLayoutId,
                        session.SeatLayout!.Name,
                        session.Status,
                        session.SeatsGenerated))
                    .ToList(),
                ev.TicketTypes
                    .OrderBy(ticketType => ticketType.Price.Amount)
                    .Select(ticketType => new TicketTypeItem(
                        ticketType.Id,
                        ticketType.Name,
                        ticketType.Price.Amount,
                        ticketType.Price.Currency,
                        ticketType.Quota,
                        ticketType.SalesStartsAtUtc,
                        ticketType.SalesEndsAtUtc,
                        ticketType.RequiresVerification,
                        ticketType.SeatSectionId,
                        context.SeatSections
                            .Where(section => section.Id == ticketType.SeatSectionId)
                            .Select(section => section.Name)
                            .FirstOrDefault(),
                        ticketType.IsActive))
                    .ToList()))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketTypeItem>> GetTicketTypesAsync(
        Guid eventId, CancellationToken cancellationToken = default) =>
        await context.TicketTypes
            .Where(ticketType => ticketType.EventId == eventId)
            .OrderBy(ticketType => ticketType.Price.Amount)
            .Select(ticketType => new TicketTypeItem(
                ticketType.Id,
                ticketType.Name,
                ticketType.Price.Amount,
                ticketType.Price.Currency,
                ticketType.Quota,
                ticketType.SalesStartsAtUtc,
                ticketType.SalesEndsAtUtc,
                ticketType.RequiresVerification,
                ticketType.SeatSectionId,
                // Bolum adi ayri sorguyla degil sol birlesimle geliyor;
                // SeatSectionId null oldugunda ad da null kalir.
                context.SeatSections
                    .Where(section => section.Id == ticketType.SeatSectionId)
                    .Select(section => section.Name)
                    .FirstOrDefault(),
                ticketType.IsActive))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EventCategoryItem>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await context.EventCategories
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new EventCategoryItem(category.Id, category.Name, category.Slug))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<SeatAvailability?> GetSeatAvailabilityAsync(
        Guid eventSessionId,
        Guid? currentUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var oturum = await context.EventSessions
            .Where(session => session.Id == eventSessionId)
            .Select(session => new
            {
                session.Id,
                session.SeatLayoutId,
                session.EventId,
                EventTitle = session.Event!.Title,
                VenueName = session.Event!.Venue!.Name,
                HallName = session.Hall!.Name,
                session.StartsAtUtc,
                session.SalesEndsAtUtc,
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (oturum is null)
            return null;

        // Tek sorgu, duz liste. Gruplama bellekte yapiliyor: PostgreSQL
        // tarafinda gruplayip her bolum icin ayri koltuk listesi cekmek
        // bolum sayisi kadar gidis donus demek olurdu.
        var koltuklar = await context.EventSeats
            .Where(seat => seat.EventSessionId == eventSessionId)
            .Select(seat => new SeatRow(
                seat.Id,
                seat.SeatId,
                seat.Seat!.SeatSectionId,
                seat.Seat!.SeatSection!.Name,
                seat.Seat!.SeatSection!.DisplayOrder,
                seat.Seat!.RowLabel,
                seat.Seat!.SeatNumber,
                seat.Seat!.PositionX,
                seat.Seat!.PositionY,
                seat.Status,
                seat.LockedUntilUtc,
                seat.LockedByUserId,
                seat.Price.Amount,
                seat.Price.Currency,
                seat.TicketType!.Name))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var bolumler = koltuklar
            .GroupBy(row => new { row.SectionId, row.SectionName, row.DisplayOrder })
            .OrderBy(grup => grup.Key.DisplayOrder)
            .ThenBy(grup => grup.Key.SectionName)
            .Select(grup => new SeatAvailabilitySection(
                grup.Key.SectionId,
                grup.Key.SectionName,
                grup.Key.DisplayOrder,
                grup
                    .OrderBy(row => row.RowLabel)
                    .ThenBy(row => row.SeatNumber)
                    .Select(row => new SeatAvailabilitySeat(
                        row.EventSeatId,
                        row.SeatId,
                        row.RowLabel,
                        row.SeatNumber,
                        row.PositionX,
                        row.PositionY,
                        // Suresi dolmus kilit istemciye BOS gosterilir:
                        // temizlik isini bekleyen koltuk satisa kapali
                        // gorunmesin. Durum degistirmek yazma islemidir,
                        // okuma ucunda yapilmaz.
                        DisplayStatus(row, utcNow),
                        row.LockedUntilUtc,
                        // Kilit sahibinin kimligi DISARI VERILMEZ; istemcinin
                        // ihtiyaci olan tek bilgi kilidin kendisine ait olup
                        // olmadigi.
                        currentUserId is not null && row.LockedByUserId == currentUserId,
                        row.PriceAmount,
                        row.PriceCurrency,
                        row.TicketTypeName))
                    .ToList()))
            .ToList();

        return new SeatAvailability(
            oturum.Id,
            oturum.SeatLayoutId,
            oturum.EventId,
            oturum.EventTitle,
            oturum.VenueName,
            oturum.HallName,
            oturum.StartsAtUtc,
            oturum.SalesEndsAtUtc,
            utcNow,
            bolumler);
    }

    private static EventSeatStatus DisplayStatus(SeatRow row, DateTime utcNow) =>
        row.Status == EventSeatStatus.Locked &&
        row.LockedUntilUtc is { } bitis &&
        utcNow >= bitis
            ? EventSeatStatus.Available
            : row.Status;

    /// <summary>
    /// Koltuk satirinin duz hâli.
    /// </summary>
    /// <remarks>
    /// Anonim tip yerine adlandirilmis kayit: <see cref="DisplayStatus"/>
    /// gibi yardimci metotlara parametre olarak gecirilebilmesi icin bir
    /// tipe ihtiyac var.
    /// </remarks>
    private sealed record SeatRow(
        Guid EventSeatId,
        Guid SeatId,
        Guid SectionId,
        string SectionName,
        int DisplayOrder,
        string RowLabel,
        int SeatNumber,
        int PositionX,
        int PositionY,
        EventSeatStatus Status,
        DateTime? LockedUntilUtc,
        Guid? LockedByUserId,
        decimal PriceAmount,
        string PriceCurrency,
        string TicketTypeName);
}
