using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface ISeatLayoutRepository
{
    /// <param name="koltuklarlaBirlikte">
    /// Gorsel plan icin koltuklar da yuklenir. Liste ekraninda gereksiz —
    /// 600 koltuklu bir planda her satirda yuzlerce kayit tasinirdi.
    /// </param>
    Task<SeatLayout?> GetByIdAsync(
        Guid id, bool koltuklarlaBirlikte = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeatLayout>> GetByHallIdAsync(
        Guid hallId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        Guid hallId, string name, Guid? haricId = null, CancellationToken cancellationToken = default);

    Task<SeatSection?> GetSectionByIdAsync(
        Guid sectionId, CancellationToken cancellationToken = default);

    Task<bool> SectionNameExistsAsync(
        Guid seatLayoutId, string name, Guid? haricId = null, CancellationToken cancellationToken = default);

    /// <summary>Plandaki toplam koltuk sayisi. Kapasite kontrolu icin.</summary>
    Task<int> CountSeatsAsync(Guid seatLayoutId, CancellationToken cancellationToken = default);

    /// <summary>Bir bolumdeki koltuk sayisi. Tekrar uretimi engellemek icin.</summary>
    Task<int> CountSeatsInSectionAsync(Guid sectionId, CancellationToken cancellationToken = default);

    Task<Seat?> GetSeatByIdAsync(Guid seatId, CancellationToken cancellationToken = default);

    void Add(SeatLayout seatLayout);

    void AddSection(SeatSection section);

    /// <summary>
    /// Toplu koltuk ekleme. Tek cagri, tek <c>SaveChanges</c>; dongude
    /// kaydetmek 600 koltukta 600 gidis donus demek olurdu.
    /// </summary>
    void AddSeats(IReadOnlyList<Seat> seats);

    /// <inheritdoc cref="IVenueRepository.Remove"/>
    void Remove(SeatLayout seatLayout);
}
