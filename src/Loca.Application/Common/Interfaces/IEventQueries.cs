using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;

namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Etkinlik okuma tarafi.
/// </summary>
/// <remarks>
/// <b>Neden repository'den ayri?</b> Yazma tarafi aggregate yukler
/// (<c>Event</c> + oturumlar + bilet turleri) cunku is kurallari nesnenin
/// tamamini gerektiriyor. Okuma tarafi ise dogrudan DTO'ya projeksiyon
/// yapar: etkinlik detayi entity olarak yuklenip sonra maplenirse kategori,
/// sehir, mekan, salon ve organizator icin ayri ayri sorgu gider — klasik
/// N+1. Projeksiyonla hepsi TEK <c>SELECT</c>'e iner.
///
/// <para>
/// Arayuz Application katmaninda, uygulamasi Persistence'ta: DTO'lar burada
/// tanimli oldugu icin Domain'deki repository arayuzu bunlari goremezdi.
/// </para>
/// </remarks>
public interface IEventQueries
{
    Task<PagedResult<EventListItem>> GetPagedAsync(
        EventListFilter filter, CancellationToken cancellationToken = default);

    Task<EventDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketTypeItem>> GetTicketTypesAsync(
        Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventCategoryItem>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    /// <param name="currentUserId">
    /// Kendi kilidini "secili", baskasinin kilidini "mesgul" gorebilmesi icin.
    /// Anonim istekte <c>null</c>.
    /// </param>
    Task<SeatAvailability?> GetSeatAvailabilityAsync(
        Guid eventSessionId,
        Guid? currentUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

/// <summary>Listeleme kisitlari.</summary>
/// <param name="OrganizerId">Verilirse yalnizca o organizatorun etkinlikleri.</param>
/// <param name="OnlyPublic">
/// Yalnizca herkese acik durumdakiler. Anonim ve musteri istekleri icin
/// <c>true</c>; aksi hâlde taslak etkinlikler de listede gorunur ve
/// organizatorun henuz duyurmadigi isi disari sizar.
/// </param>
/// <param name="CategoryId">
/// Verilirse yalnizca o kategorideki etkinlikler doner.
/// <b>Suzme sunucuda yapiliyor, istemcide degil.</b> Istemcide yapilsaydi
/// yalnizca o an cekilmis sayfa suzulur, katalogun geri kalani filtrenin
/// disinda kalirdi — kullanici "bu kategoride 3 etkinlik var" sanardi.
/// Veritabanindaki (CityId, CategoryId, EventDateUtc) index'i zaten bu
/// sorgu icin kuruldu.
/// </param>
/// <param name="Search">
/// Etkinlik basliginda gecen metin. Buyuk/kucuk harf duyarsiz.
/// <c>ToLower</c> DEGIL <c>ILIKE</c> kullaniliyor: <c>ToLower</c> kolon
/// uzerinde fonksiyon cagirdigi icin index'i devre disi birakiyor ve
/// calisan makinenin kulturune bagli — Turkce kulturde buyuk I noktasiz
/// i'ye donuyor ve ISTANBUL ile istanbul beklenmedik bicimde farkli
/// sayilabiliyordu (bkz. Gun 4, 14 numarali sorun).
/// </param>
public sealed record EventListFilter(
    Guid? OrganizerId,
    bool OnlyPublic,
    PaginationRequest Pagination,
    Guid? CategoryId = null,
    string? Search = null);
