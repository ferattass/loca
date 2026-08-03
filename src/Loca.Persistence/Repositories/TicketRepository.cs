using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class TicketRepository(LocaDbContext context) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    public Task<Ticket?> GetByQrCodeAsync(
        string qrCode, CancellationToken cancellationToken = default) =>
        context.Tickets.FirstOrDefaultAsync(ticket => ticket.QrCode == qrCode, cancellationToken);

    public async Task<bool> ExistsForReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        await context.Tickets.AnyAsync(
            ticket => ticket.ReservationId == reservationId, cancellationToken);

    public async Task<IReadOnlyList<Ticket>> GetByReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        await context.Tickets
            .Where(ticket => ticket.ReservationId == reservationId)
            .ToListAsync(cancellationToken);

    public void AddRange(IReadOnlyList<Ticket> tickets) => context.Tickets.AddRange(tickets);
}
