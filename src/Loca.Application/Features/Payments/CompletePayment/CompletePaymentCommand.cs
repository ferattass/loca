using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
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
    ITicketRepository tickets,
    PaymentFinalizer finalizer,
    IPaymentService paymentService,
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

        // --- Havale bu yoldan GECEMEZ ---------------------------------------
        // Bu uc, sonucu SAGLAYICIYA sorarak dogruluyor. Havalenin karsiliginda
        // sorulacak bir saglayici yok; onayi yonetici veriyor
        // (ConfirmBankTransferCommand). Bu kontrol olmasaydi kullanici, havale
        // ile actigi odeme icin bu ucu cagirir ve o an calisan saglayici
        // (yerelde taklit saglayici) "basarili" deyip parasi hic gelmemis bir
        // rezervasyona bilet uretirdi.
        if (odeme.IsBankTransfer)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.BankTransferNotCompletable);

        if (!odeme.IsPending)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.ProviderRejected);

        // --- Saglayiciya sor ------------------------------------------------
        var sonuc = await paymentService.VerifyPaymentAsync(
            odeme.Id, odeme.ProviderReference, cancellationToken);

        // Basarisiz odeme AYRI bir komutta islenmiyor: saglayici "hayir"
        // dediginde koltuklarin hemen serbest kalmasi gerekiyor ve bunu
        // ayri bir istegin gelmesine birakmak, koltuklari kilit suresi
        // dolana kadar bloke tutardi.
        return sonuc.Succeeded
            ? await finalizer.CloseAsSucceededAsync(odeme, sonuc.Reference, utcNow, cancellationToken)
            : await finalizer.CloseAsFailedAsync(
                odeme,
                sonuc.FailureReason ?? "Saglayici islemi reddetti.",
                utcNow,
                cancellationToken);
    }

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
