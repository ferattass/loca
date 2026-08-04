using Loca.Application.Features.Tickets.Common;

namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Bilet okuma tarafi; projeksiyonla tek <c>SELECT</c>.
/// </summary>
public interface ITicketQueries
{
    /// <param name="reservationId">
    /// Verilirse yalnizca o rezervasyonun biletleri doner.
    /// </param>
    /// <remarks>
    /// Kullanici filtresi her durumda uygulanir; rezervasyon filtresi onun
    /// yerine degil ustune gelir. Aksi halde baska bir rezervasyonun
    /// kimligini yazan biri o rezervasyonun biletlerini okuyabilirdi.
    /// </remarks>
    Task<IReadOnlyList<TicketDetail>> GetByUserAsync(
        Guid userId,
        Guid? reservationId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<TicketDetail?> GetByIdAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default);
}
