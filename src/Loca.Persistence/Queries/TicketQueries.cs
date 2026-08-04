using Loca.Application.Common.Interfaces;
using Loca.Application.Features.Tickets.Common;
using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

/// <summary>
/// Bilet okuma sorgulari; projeksiyonla tek <c>SELECT</c>.
/// </summary>
internal sealed class TicketQueries(LocaDbContext context) : ITicketQueries
{
    public async Task<IReadOnlyList<TicketDetail>> GetByUserAsync(
        Guid userId,
        Guid? reservationId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var sorgu = context.Tickets.Where(ticket => ticket.UserId == userId);

        // Rezervasyon filtresi kullanici filtresinin YERINE degil USTUNE
        // geliyor; tek basina birakilsaydi baska bir rezervasyonun kimligini
        // yazan biri o biletlerin QR kodlarini okuyabilirdi.
        if (reservationId is { } rezervasyon)
            sorgu = sorgu.Where(ticket => ticket.ReservationId == rezervasyon);

        var biletler = await Projeksiyon(sorgu).ToListAsync(cancellationToken);

        // Yaklasan etkinlikler en yakindan, gecmistekiler en yeniden.
        // Tek bir ORDER BY bunu veremiyor cunku iki grubun yonu ters; liste
        // kullanici basina kucuk oldugu icin ayirma bellekte yapiliyor.
        return
        [
            .. biletler.Where(bilet => bilet.EventStartsAtUtc >= utcNow),
            .. biletler.Where(bilet => bilet.EventStartsAtUtc < utcNow).Reverse()
        ];
    }

    public Task<TicketDetail?> GetByIdAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Projeksiyon(context.Tickets.Where(ticket => ticket.Id == id && ticket.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <remarks>
    /// Mekân ve salon adi biletin kendi satirinda degil; oturum uzerinden
    /// birlestiriliyor. <see cref="Ticket"/> uzerinde <c>EventSession</c>
    /// gezinti ozelligi yok, bu yuzden birlestirme elle yaziliyor.
    ///
    /// <para>
    /// Siralama <b>projeksiyondan ONCE</b>: <c>TicketDetail</c> uzerinden
    /// siralamak EF'in ceviremedigi bir ifade uretiyor ("could not be
    /// translated"), cunku kayit yapicisi SQL'de bir kolona karsilik
    /// gelmiyor. Ara adimda anonim tip kullanildigi icin siralama gercek
    /// kolona bakiyor.
    /// </para>
    /// </remarks>
    private IQueryable<TicketDetail> Projeksiyon(IQueryable<Ticket> sorgu) =>
        sorgu
            .Join(
                context.EventSessions,
                ticket => ticket.EventSessionId,
                session => session.Id,
                (ticket, session) => new { ticket, session })
            .OrderBy(satir => satir.ticket.EventStartsAtUtc)
            .Select(satir => new TicketDetail(
                satir.ticket.Id,
                satir.ticket.ReservationId,
                satir.ticket.EventId,
                satir.ticket.EventSessionId,
                satir.ticket.TicketNumber,
                satir.ticket.QrCode,
                satir.ticket.EventTitle,
                satir.session.Event!.Venue!.Name,
                satir.session.Hall!.Name,
                satir.ticket.SeatLabel,
                satir.ticket.TicketTypeName,
                satir.ticket.EventStartsAtUtc,
                satir.ticket.Price.Amount,
                satir.ticket.Price.Currency,
                satir.ticket.Status,
                satir.ticket.IssuedAtUtc))
            .AsNoTracking();
}
