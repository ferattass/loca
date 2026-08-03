using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Giriste okutulan kodla bilet arar.</summary>
    Task<Ticket?> GetByQrCodeAsync(string qrCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu rezervasyon icin bilet uretilmis mi.
    /// </summary>
    /// <remarks>
    /// Tekrar eden odeme bildiriminin ikinci kez bilet uretmesini engelleyen
    /// kontrol. Odemenin durumu da ayni sonucu veriyor ama iki kontrol
    /// birbirinden bagimsiz: biri odeme kaydina, digeri gercekten uretilmis
    /// satirlara bakiyor.
    /// </remarks>
    Task<bool> ExistsForReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default);

    void AddRange(IReadOnlyList<Ticket> tickets);
}
