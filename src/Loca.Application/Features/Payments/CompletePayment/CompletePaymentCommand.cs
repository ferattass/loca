using System.Text.Json;
using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Payments.CompletePayment;

/// <summary>
/// Odemenin sonucunu saglayiciya sorar; basariliysa bilet uretir.
/// </summary>
/// <remarks>
/// <b>Callback'e guvenilmiyor.</b> Bildirim kaybolabilir, gecikebilir veya
/// taklit edilebilir; bu yuzden odeme tamamlanmadan once durum SAGLAYICIYA
/// SORULUYOR. Istemcinin "odeme basarili" demesi tek basina yeterli olsaydi,
/// istegi elle olusturan biri odemeden bilet alabilirdi.
///
/// <para>
/// <b>Ikinci callback hicbir seyi degistirmez.</b> Odeme saglayicilari ayni
/// bildirimi birden fazla kez gonderir. <see cref="Payment.Complete"/>
/// "durum degisti mi" bilgisini donduruyor; yan etkiler (rezervasyon onayi,
/// bilet uretimi, koltuklarin satilmasi) yalnizca DEGISTIYSE calisiyor.
/// </para>
/// </remarks>
public sealed record CompletePaymentCommand(Guid PaymentId)
    : IRequest<Result<PaymentCompletionResult>>;

internal sealed class CompletePaymentCommandHandler(
    IPaymentRepository payments,
    IReservationRepository reservations,
    ITicketRepository tickets,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    IPaymentService paymentService,
    ITicketCodeGenerator kodUretici,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<CompletePaymentCommandHandler> logger)
    : IRequestHandler<CompletePaymentCommand, Result<PaymentCompletionResult>>
{
    public async Task<Result<PaymentCompletionResult>> Handle(
        CompletePaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is null)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.Unauthenticated);

        var odeme = await payments.GetAggregateAsync(request.PaymentId, cancellationToken);

        if (odeme is null)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotFound);

        if (!Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), odeme))
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotOwner);

        var utcNow = clock.UtcNow;

        // --- Tekrar eden bildirim ------------------------------------------
        // Odeme zaten basariliysa saglayiciya bile sorulmuyor: sonuc belli ve
        // degistirilemez. Mevcut biletler donuyor.
        if (odeme.IsSuccessful)
        {
            var mevcutBiletler = await tickets.GetByReservationAsync(
                odeme.ReservationId, cancellationToken);

            logger.LogInformation(
                "Tekrar eden odeme bildirimi; durum degismedi. OdemeId: {OdemeId}", odeme.Id);

            return Result.Success(new PaymentCompletionResult(
                odeme.Id, odeme.Status, odeme.ReservationId, false, Bilete(mevcutBiletler)));
        }

        if (!odeme.IsPending)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.ProviderRejected);

        // --- Saglayiciya sor ------------------------------------------------
        var sonuc = await paymentService.VerifyPaymentAsync(
            odeme.Id, odeme.ProviderReference, cancellationToken);

        if (!sonuc.Succeeded)
        {
            // Basarisiz odeme AYRI bir komutta islenmiyor: saglayici "hayir"
            // dediginde koltuklarin hemen serbest kalmasi gerekiyor ve bunu
            // ayri bir istegin gelmesine birakmak, koltuklari kilit suresi
            // dolana kadar bloke tutardi.
            return await BasarisizKapatAsync(
                odeme, sonuc.FailureReason ?? "Saglayici islemi reddetti.", utcNow, cancellationToken);
        }

        return await BasariliKapatAsync(odeme, sonuc.Reference, utcNow, cancellationToken);
    }

    /// <summary>
    /// Odeme basarili: rezervasyon onaylanir, biletler uretilir, koltuklar satilir.
    /// </summary>
    /// <remarks>
    /// <b>Hepsi TEK transaction icinde.</b> Bilet uretimi transaction disina
    /// alinsaydi, arada olusan bir hata "parasi alinmis ama bileti olmayan"
    /// bir kullanici birakirdi — bu akista geri alinmasi en pahali durum.
    /// </remarks>
    private async Task<Result<PaymentCompletionResult>> BasariliKapatAsync(
        Payment odeme, string? referans, DateTime utcNow, CancellationToken cancellationToken)
    {
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

        // Koltuklarin hâlâ bu rezervasyonda oldugu dogrulanıyor. Kilit suresi
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
            "Bilet: {BiletSayisi}, Tutar: {Tutar}",
            odeme.Id,
            rezervasyon.Id,
            uretilen.Count,
            odeme.Amount);

        return Result.Success(new PaymentCompletionResult(
            odeme.Id, odeme.Status, rezervasyon.Id, true, Bilete(uretilen)));
    }

    /// <summary>
    /// Odeme basarisiz: rezervasyon iptal edilir, koltuklar HEMEN serbest kalir.
    /// </summary>
    private async Task<Result<PaymentCompletionResult>> BasarisizKapatAsync(
        Payment odeme, string sebep, DateTime utcNow, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        odeme.Fail(sebep, utcNow);
        payments.RegisterNewTransactions(odeme);

        var rezervasyon = await reservations.GetAggregateAsync(odeme.ReservationId, cancellationToken);

        if (rezervasyon is not null && rezervasyon.Status == Domain.Enums.ReservationStatus.Pending)
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
