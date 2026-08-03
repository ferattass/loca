using FluentValidation;
using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Payments.StartPayment;

/// <summary>
/// Rezervasyon icin odeme baslatir.
/// </summary>
/// <remarks>
/// <b>Tutar istekten alinmiyor</b>, rezervasyonun kendi toplamindan
/// kopyalaniyor. Istekte tasinsaydi araya giren biri dokuz yuz liralik
/// rezervasyonu bir liraya odeyebilirdi.
/// </remarks>
public sealed record StartPaymentCommand(Guid ReservationId, string IdempotencyKey)
    : IRequest<Result<PaymentDetail>>;

public sealed class StartPaymentCommandValidator : AbstractValidator<StartPaymentCommand>
{
    public StartPaymentCommandValidator()
    {
        RuleFor(command => command.ReservationId).NotEmpty();

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key basligi zorunludur.")
            .MaximumLength(100);
    }
}

internal sealed class StartPaymentCommandHandler(
    IPaymentRepository payments,
    IReservationRepository reservations,
    IUnitOfWork unitOfWork,
    IPaymentService paymentService,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<StartPaymentCommandHandler> logger)
    : IRequestHandler<StartPaymentCommand, Result<PaymentDetail>>
{
    public async Task<Result<PaymentDetail>> Handle(
        StartPaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<PaymentDetail>(PaymentErrors.Unauthenticated);

        var utcNow = clock.UtcNow;

        // Ayni anahtarla gelen ikinci istek yeni odeme acmaz.
        var mevcut = await payments.GetByIdempotencyKeyAsync(
            userId, request.IdempotencyKey, cancellationToken);

        if (mevcut is not null)
            return Result.Success(Detay(mevcut, null));

        var rezervasyon = await reservations.GetAggregateAsync(
            request.ReservationId, cancellationToken);

        if (rezervasyon is null)
            return Result.Failure<PaymentDetail>(PaymentErrors.ReservationNotFound);

        if (!Ownership.Allows(userId, currentUser.IsInRole(RoleNames.Admin), rezervasyon))
            return Result.Failure<PaymentDetail>(PaymentErrors.NotOwner);

        // Suresi dolmus rezervasyonun odemesi baslatilmaz: koltuklar bu arada
        // baskasina gitmis olabilir.
        if (!rezervasyon.IsActive(utcNow))
            return Result.Failure<PaymentDetail>(PaymentErrors.ReservationNotActive);

        if (await payments.GetSuccessfulByReservationAsync(rezervasyon.Id, cancellationToken) is not null)
            return Result.Failure<PaymentDetail>(PaymentErrors.AlreadyPaid);

        if (await payments.GetPendingByReservationAsync(rezervasyon.Id, cancellationToken) is not null)
            return Result.Failure<PaymentDetail>(PaymentErrors.AlreadyPending);

        var odeme = new Payment(
            rezervasyon.Id,
            userId,
            rezervasyon.TotalAmount,
            paymentService.Name,
            request.IdempotencyKey,
            utcNow);

        // Saglayiciya once kayit acilip sonra veritabanina yazilmiyor: sira
        // ters olsaydi saglayicida acilmis ama bizde karsiligi olmayan bir
        // islem kalabilirdi. Once kendi kaydimizi yaziyoruz.
        payments.Add(odeme);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var sonuc = await paymentService.CreatePaymentAsync(
            odeme.Id, odeme.Amount.Amount, odeme.Amount.Currency, cancellationToken);

        if (!sonuc.Succeeded)
        {
            odeme.Fail(sonuc.FailureReason ?? "Odeme baslatilamadi.", clock.UtcNow);
            payments.RegisterNewTransactions(odeme);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Odeme baslatilamadi. OdemeId: {OdemeId}, Sebep: {Sebep}",
                odeme.Id,
                sonuc.FailureReason);

            return Result.Failure<PaymentDetail>(PaymentErrors.ProviderRejected);
        }

        // Saglayici kimligi SAKLANIYOR: bir sonraki adimda "bu odemenin
        // durumu ne" sorusu ona dayanarak soruluyor.
        if (sonuc.Reference is { Length: > 0 } referans)
        {
            odeme.AttachProviderReference(referans);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Odeme baslatildi. OdemeId: {OdemeId}, RezervasyonId: {RezervasyonId}, Tutar: {Tutar}",
            odeme.Id,
            rezervasyon.Id,
            odeme.Amount);

        return Result.Success(Detay(odeme, sonuc.Reference));
    }

    /// <param name="yonlendirme">
    /// Saglayicinin odeme sayfasi. Taklit saglayicida anlamli bir adres yok.
    /// </param>
    private static PaymentDetail Detay(Payment odeme, string? yonlendirme) =>
        new(
            odeme.Id,
            odeme.ReservationId,
            odeme.Status,
            odeme.Provider,
            odeme.ProviderReference,
            yonlendirme is not null && yonlendirme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? yonlendirme
                : null,
            odeme.Amount.Amount,
            odeme.Amount.Currency,
            odeme.CompletedAtUtc,
            odeme.FailureReason,
            odeme.CreatedAt,
            odeme.Transactions
                .OrderBy(islem => islem.OccurredAtUtc)
                .Select(islem => new PaymentAttempt(islem.Type, islem.OccurredAtUtc, islem.Message))
                .ToList());
}
