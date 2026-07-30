using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Events.PublishEvent;

/// <summary>
/// Admin onayi: PendingApproval → Published. Ayni islemde satilabilir
/// koltuklar (<c>EventSeats</c>) uretilir.
/// </summary>
/// <remarks>
/// Koltuk uretiminin yayin aninda olmasi bilincli: taslak asamasinda
/// uretilseydi organizator oturma planini degistirdiginde binlerce satir
/// bosa cikardi. Yayin, planin artik degismeyecegi andir.
/// </remarks>
public sealed record PublishEventCommand(Guid EventId) : IRequest<Result<PublishEventResult>>;

/// <param name="GeneratedSeatCount">
/// Uretilen koltuk sayisi. Kabul testinde 20x30 = 600 bekleniyor.
/// </param>
public sealed record PublishEventResult(
    Guid EventId,
    int SessionCount,
    int GeneratedSeatCount);

internal sealed class PublishEventCommandHandler(
    IEventRepository events,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<PublishEventCommandHandler> logger)
    : IRequestHandler<PublishEventCommand, Result<PublishEventResult>>
{
    public async Task<Result<PublishEventResult>> Handle(
        PublishEventCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ev = await events.GetAggregateAsync(request.EventId, cancellationToken);

        if (ev is null)
            return Result.Failure<PublishEventResult>(EventErrors.NotFound);

        // Durum gecisi ve yayin on kosullari domain'de. Ihlal DomainException
        // olarak cikar ve 409'a cevrilir.
        ev.Publish(clock.UtcNow);

        var uretilecek = new List<EventSeat>();

        var oturumlar = ev.Sessions
            .Where(session => session.Status != EventSessionStatus.Cancelled)
            .ToList();

        foreach (var session in oturumlar)
        {
            // Iki katmanli tekrar korumasi. Bayrak aggregate'te, sorgu ise
            // veritabaninda: bayrak bir sekilde sifirlanmis olsa bile ayni
            // koltuk ikinci kez uretilmeye calisildiginda
            // UNIQUE(EventSessionId, SeatId) son savunma hatti olarak devreye
            // girer ve islem tamamen basarisiz olur.
            if (session.SeatsGenerated)
                continue;

            if (await events.SeatsGeneratedForSessionAsync(session.Id, cancellationToken))
                continue;

            var sonuc = await BuildSeatsForSessionAsync(ev, session, cancellationToken);

            if (sonuc.IsFailure)
                return Result.Failure<PublishEventResult>(sonuc.Error);

            uretilecek.AddRange(sonuc.Value);
            session.MarkSeatsGenerated();
        }

        if (uretilecek.Count > 0)
            events.AddEventSeats(uretilecek);

        // TEK SaveChanges = TEK transaction. EF cagriyi kendisi bir
        // transaction icine aliyor; durum degisikligi, oturum bayraklari ve
        // binlerce koltuk ya tamamen yazilir ya hic yazilmaz. Ayri ayri
        // kaydedilseydi araya giren bir hata "yayinda ama koltuksuz" bir
        // etkinlik birakabilirdi.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Etkinlik yayina alindi. EtkinlikId: {EtkinlikId}, Oturum: {OturumSayisi}, " +
            "UretilenKoltuk: {KoltukSayisi}",
            ev.Id,
            oturumlar.Count,
            uretilecek.Count);

        return Result.Success(new PublishEventResult(ev.Id, oturumlar.Count, uretilecek.Count));
    }

    /// <summary>
    /// Bir oturumun koltuklarini kurar; henuz veritabanina yazmaz.
    /// </summary>
    private async Task<Result<List<EventSeat>>> BuildSeatsForSessionAsync(
        Event ev,
        EventSession session,
        CancellationToken cancellationToken)
    {
        var koltuklar = await events.GetActiveSeatPlacementsAsync(
            session.SeatLayoutId, cancellationToken);

        if (koltuklar.Count == 0)
            return Result.Failure<List<EventSeat>>(EventErrors.NoSeatsInLayout);

        // Bolume atanmis aktif turler. Ayni bolumun iki aktif ture atanmasi
        // aggregate icinde engellendigi icin burada ToDictionary guvenli;
        // engellenmese "ayni anahtar" hatasi calisma aninda cikardi.
        var bolumBazliTurler = ev.TicketTypes
            .Where(ticketType => ticketType.IsActive && ticketType.SeatSectionId is not null)
            .ToDictionary(ticketType => ticketType.SeatSectionId!.Value);

        var varsayilan = ev.DefaultTicketType;

        var eslesmeyenBolum = koltuklar
            .Select(koltuk => koltuk.SeatSectionId)
            .Distinct()
            .Any(bolum => !bolumBazliTurler.ContainsKey(bolum));

        // Eslesmeyen bolum varsa varsayilan tur zorunlu: aksi hâlde o
        // bolumun koltuklari fiyatsiz kalir ve satin alma aninda tutar
        // hesaplanamaz.
        if (eslesmeyenBolum && varsayilan is null)
            return Result.Failure<List<EventSeat>>(EventErrors.NoDefaultTicketType);

        var uretilen = new List<EventSeat>(koltuklar.Count);

        foreach (var koltuk in koltuklar)
        {
            var tur = bolumBazliTurler.TryGetValue(koltuk.SeatSectionId, out var bolumTuru)
                ? bolumTuru
                : varsayilan!;

            // FIYAT KOPYALANIR, referans verilmez. Bilet turunun fiyati
            // sonradan degisirse gecmiste satilmis biletin tutari
            // degismemeli — muhasebe acisindan zorunlu.
            uretilen.Add(new EventSeat(session.Id, koltuk.SeatId, tur.Id, tur.Price));
        }

        return Result.Success(uretilen);
    }
}
