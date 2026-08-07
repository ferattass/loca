using System.Text.Json;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Payments.Common;

/// <summary>
/// Bir odemenin kapanisi: basariliysa bilet uretir, basarisizsa koltuklari birakir.
/// </summary>
/// <remarks>
/// <b>Neden ayri bir sinif:</b> ayni kapanis iki farkli yerden tetikleniyor —
/// saglayicinin cevabiyla (<c>CompletePaymentCommand</c>) ve yoneticinin
/// havale onayiyla (<c>ConfirmBankTransferCommand</c>). Kod iki handler'a
/// kopyalansaydi bilet uretimi — akistaki en pahali ve geri alinmasi en zor
/// adim — iki ayri yerde yasar, birinde yapilan duzeltme digerinde unutulurdu.
///
/// <para>
/// Handler degil dogrudan cagrilan bir servis: MediatR uzerinden gitseydi
/// ic ice bir istek olur, dogrulama ve loglama davranislari ikinci kez
/// kosardi.
/// </para>
/// </remarks>
internal sealed class PaymentFinalizer(
    IPaymentRepository payments,
    IReservationRepository reservations,
    ITicketRepository tickets,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    ITicketCodeGenerator kodUretici,
    ILogger<PaymentFinalizer> logger)
{
    /// <summary>
    /// Odeme basarili: rezervasyon onaylanir, biletler uretilir, koltuklar satilir.
    /// </summary>
    /// <remarks>
    /// <b>Hepsi TEK transaction icinde.</b> Bilet uretimi transaction disina
    /// alinsaydi, arada olusan bir hata "parasi alinmis ama bileti olmayan"
    /// bir kullanici birakirdi — bu akista geri alinmasi en pahali durum.
    /// </remarks>
    public async Task<Result<PaymentCompletionResult>> CloseAsSucceededAsync(
        Payment odeme, string? referans, DateTime utcNow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(odeme);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var rezervasyon = await reservations.GetAggregateAsync(odeme.ReservationId, cancellationToken);

        if (rezervasyon is null)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.ReservationNotFound);

        // Bilet zaten uretilmisse sessizce ikinci kez uretilmiyor. Uygulama
        // katmanindaki bu kontrolun yani sira ReservationItemId uzerindeki
        // tekil kisit son savunma hatti olarak duruyor.
        if (await tickets.ExistsForReservationAsync(rezervasyon.Id, cancellationToken))
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.TicketsAlreadyIssued);

        var koltuklar = await reservations.GetSeatsOfReservationAsync(
            rezervasyon.Id, cancellationToken);

        // Koltuklarin hâlâ bu rezervasyonda oldugu dogrulaniyor. Kilit suresi
        // dolup koltuk baskasina gectiyse liste eksik doner; odemeyi
        // tamamlamak, olmayan koltuk icin bilet kesmek olurdu.
        if (koltuklar.Count != rezervasyon.SeatCount)
        {
            logger.LogWarning(
                "Odeme tamamlanamadi: koltuklar artik tutulmuyor. RezervasyonId: {RezervasyonId}, " +
                "Beklenen: {Beklenen}, Bulunan: {Bulunan}",
                rezervasyon.Id,
                rezervasyon.SeatCount,
                koltuklar.Count);

            return Result.Failure<PaymentCompletionResult>(PaymentErrors.SeatsNoLongerHeld);
        }

        var kaynaklar = await payments.GetTicketSourcesAsync(rezervasyon.Id, cancellationToken);

        // Durum gecisleri domain'de; ihlal DomainException → 409.
        odeme.Complete(referans, utcNow);

        // Domain metodu bir islem satiri ekledi; EF'e bunun YENI oldugu
        // acikca bildiriliyor (bkz. IPaymentRepository.RegisterNewTransactions).
        payments.RegisterNewTransactions(odeme);

        rezervasyon.Confirm(utcNow);

        foreach (var koltuk in koltuklar)
        {
            // Locked → Reserved → Sold. Ara adim atlanmiyor cunku gecisler
            // entity icinde birbirine bagli; dogrudan Sold'a gecmek domain
            // kuralini delerdi.
            koltuk.AttachToReservation(rezervasyon.Id);
            koltuk.MarkSold();
        }

        var uretilen = new List<Ticket>(kaynaklar.Count);

        foreach (var kaynak in kaynaklar)
        {
            uretilen.Add(new Ticket(
                rezervasyon.Id,
                kaynak.ReservationItemId,
                rezervasyon.UserId,
                kaynak.EventId,
                kaynak.EventSessionId,
                kaynak.EventSeatId,
                kaynak.TicketTypeId,
                kodUretici.NewTicketNumber(),
                kodUretici.NewQrCode(),
                kaynak.EventTitle,
                $"{kaynak.SectionName} {kaynak.RowLabel}-{kaynak.SeatNumber}",
                kaynak.TicketTypeName,
                kaynak.EventStartsAtUtc,
                kaynak.Price,
                utcNow));
        }

        tickets.AddRange(uretilen);

        // Bildirim BURADAN gonderilmiyor, kuyruga yaziliyor. Gonderim
        // transaction icinde yapilsaydi yavas bir e-posta sunucusu
        // transaction'i acik tutar, hata verdiginde ise tamamlanmis bir
        // odemeyi geri alirdi.
        outbox.Add(new OutboxMessage(
            "TicketsIssued",
            JsonSerializer.Serialize(new
            {
                reservationId = rezervasyon.Id,
                paymentId = odeme.Id,
                userId = rezervasyon.UserId,
                ticketCount = uretilen.Count,
                totalAmount = rezervasyon.TotalAmount.Amount,
                currency = rezervasyon.TotalAmount.Currency,
            }),
            utcNow,
            rezervasyon.Id));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Odeme tamamlandi. OdemeId: {OdemeId}, RezervasyonId: {RezervasyonId}, " +
            "Yontem: {Yontem}, Bilet: {BiletSayisi}, Tutar: {Tutar}",
            odeme.Id,
            rezervasyon.Id,
            odeme.Method,
            uretilen.Count,
            odeme.Amount);

        return Result.Success(new PaymentCompletionResult(
            odeme.Id, odeme.Status, rezervasyon.Id, true, Bilete(uretilen)));
    }

    /// <summary>
    /// Odeme basarisiz: rezervasyon iptal edilir, koltuklar HEMEN serbest kalir.
    /// </summary>
    public async Task<Result<PaymentCompletionResult>> CloseAsFailedAsync(
        Payment odeme, string sebep, DateTime utcNow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(odeme);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        odeme.Fail(sebep, utcNow);
        payments.RegisterNewTransactions(odeme);

        var rezervasyon = await reservations.GetAggregateAsync(odeme.ReservationId, cancellationToken);

        if (rezervasyon is not null && rezervasyon.Status == ReservationStatus.Pending)
        {
            rezervasyon.Cancel(utcNow);

            var koltuklar = await reservations.GetSeatsOfReservationAsync(
                rezervasyon.Id, cancellationToken);

            foreach (var koltuk in koltuklar)
                koltuk.Release();
        }

        outbox.Add(new OutboxMessage(
            "PaymentFailed",
            JsonSerializer.Serialize(new
            {
                paymentId = odeme.Id,
                reservationId = odeme.ReservationId,
                userId = odeme.UserId,
                reason = sebep,
            }),
            utcNow,
            odeme.ReservationId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Odeme basarisiz, koltuklar serbest birakildi. OdemeId: {OdemeId}, Sebep: {Sebep}",
            odeme.Id,
            sebep);

        return Result.Success(new PaymentCompletionResult(
            odeme.Id, odeme.Status, odeme.ReservationId, true, []));
    }

    // Donus tipi arayuz degil somut liste: metot private ve cagrilan yer
    // zaten arayuze yukseltiyor (CA1859).
    private static List<IssuedTicket> Bilete(IReadOnlyList<Ticket> biletler) =>
        biletler
            .Select(bilet => new IssuedTicket(
                bilet.Id,
                bilet.TicketNumber,
                bilet.QrCode,
                bilet.EventTitle,
                bilet.SeatLabel,
                bilet.TicketTypeName,
                bilet.EventStartsAtUtc,
                bilet.Price.Amount,
                bilet.Price.Currency))
            .ToList();
}
