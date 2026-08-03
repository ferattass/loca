using System.Text.Json;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Payments.RefundPayment;

/// <summary>
/// Tahsil edilmis odemeyi iade eder; biletler ve koltuklar geri alinir.
/// </summary>
/// <remarks>
/// Yalnizca <b>admin</b> calistirir. Kullanicinin kendi kendine iade
/// baslatmasi, iade politikasinin (etkinlikten 24 saat oncesine kadar)
/// kontrol edilmesini gerektirir; o kural Gun 9'un konusu. Bu uc su an
/// operasyonel bir arac.
/// </remarks>
public sealed record RefundPaymentCommand(Guid PaymentId, string? Reason) : IRequest<Result>;

internal sealed class RefundPaymentCommandHandler(
    IPaymentRepository payments,
    IReservationRepository reservations,
    ITicketRepository tickets,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    IPaymentService paymentService,
    IDateTimeProvider clock,
    ILogger<RefundPaymentCommandHandler> logger)
    : IRequestHandler<RefundPaymentCommand, Result>
{
    public async Task<Result> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var odeme = await payments.GetAggregateAsync(request.PaymentId, cancellationToken);

        if (odeme is null)
            return Result.Failure(PaymentErrors.NotFound);

        if (!odeme.IsSuccessful)
            return Result.Failure(PaymentErrors.NotRefundable);

        var utcNow = clock.UtcNow;

        // SAGLAYICI ONCE. Iade once bizde isaretlenip sonra saglayiciya
        // gonderilseydi, saglayici reddettiginde bizde "iade edildi" gorunen
        // ama parasi geri gitmemis bir kayit kalirdi.
        var sonuc = await paymentService.RefundPaymentAsync(
            odeme.Id, odeme.ProviderReference, odeme.Amount.Amount, cancellationToken);

        if (!sonuc.Succeeded)
        {
            logger.LogWarning(
                "Iade saglayici tarafindan reddedildi. OdemeId: {OdemeId}, Sebep: {Sebep}",
                odeme.Id,
                sonuc.FailureReason);

            return Result.Failure(PaymentErrors.ProviderRejected);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        odeme.Refund(utcNow, request.Reason);
        payments.RegisterNewTransactions(odeme);

        // Rezervasyon da kapaniyor: odemesi iade edilmis bir rezervasyonun
        // "onaylanmis" gorunmesi raporlarda gercek olmayan bir satis yaratirdi.
        var rezervasyon = await reservations.GetAggregateAsync(odeme.ReservationId, cancellationToken);
        rezervasyon?.MarkRefunded(utcNow);

        var biletler = await tickets.GetByReservationAsync(odeme.ReservationId, cancellationToken);

        foreach (var bilet in biletler)
            bilet.MarkRefunded(utcNow);

        // Koltuklar SATISA GERI ACILIYOR: iade edilmis bir koltugu satilmis
        // tutmak, salonun bir kismini kalici olarak kaybetmek demek.
        var koltuklar = await reservations.GetSeatsOfReservationAsync(
            odeme.ReservationId, cancellationToken);

        foreach (var koltuk in koltuklar)
            koltuk.ReturnToSaleAfterRefund();

        outbox.Add(new OutboxMessage(
            "PaymentRefunded",
            JsonSerializer.Serialize(new
            {
                paymentId = odeme.Id,
                reservationId = odeme.ReservationId,
                userId = odeme.UserId,
                amount = odeme.Amount.Amount,
                currency = odeme.Amount.Currency,
                ticketCount = biletler.Count,
            }),
            utcNow,
            odeme.ReservationId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Odeme iade edildi. OdemeId: {OdemeId}, Bilet: {BiletSayisi}, Koltuk: {KoltukSayisi}",
            odeme.Id,
            biletler.Count,
            koltuklar.Count);

        return Result.Success();
    }
}
