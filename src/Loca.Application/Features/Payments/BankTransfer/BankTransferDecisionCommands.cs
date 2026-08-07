using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Payments.BankTransfer;

/// <summary>
/// Yonetici havalenin hesaba gectigini onaylar; biletler uretilir.
/// </summary>
/// <remarks>
/// <b>Bu komut kart odemesine uygulanamaz.</b> Kartin sonucu saglayiciya
/// SORULARAK dogrulaniyor; elle onaylanabilseydi panele erisen biri parasi
/// hic cekilmemis bir karti "odendi" yapabilirdi. Havalede boyle bir soru
/// mercii yok — paranin geldigini yalnizca hesaba bakan insan bilir — bu
/// yuzden onay yoneticide ve yalnizca havalede.
/// </remarks>
/// <param name="Reference">
/// Ekstredeki islem numarasi. Zorunlu degil ama mutabakatta tek baglanti
/// noktasi: hangi banka hareketine bakip onaylandigi sonradan buradan
/// okunuyor.
/// </param>
public sealed record ConfirmBankTransferCommand(Guid PaymentId, string? Reference)
    : IRequest<Result<PaymentCompletionResult>>;

internal sealed class ConfirmBankTransferCommandValidator
    : AbstractValidator<ConfirmBankTransferCommand>
{
    public ConfirmBankTransferCommandValidator()
    {
        RuleFor(komut => komut.PaymentId).NotEmpty();
        RuleFor(komut => komut.Reference).MaximumLength(100);
    }
}

internal sealed class ConfirmBankTransferCommandHandler(
    IPaymentRepository payments,
    PaymentFinalizer finalizer,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<ConfirmBankTransferCommandHandler> logger)
    : IRequestHandler<ConfirmBankTransferCommand, Result<PaymentCompletionResult>>
{
    public async Task<Result<PaymentCompletionResult>> Handle(
        ConfirmBankTransferCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var odeme = await payments.GetAggregateAsync(request.PaymentId, cancellationToken);

        if (odeme is null)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotFound);

        if (!odeme.IsBankTransfer)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotBankTransfer);

        // Zaten onaylanmis kaydin ikinci onayi hata degil: yonetici iki kez
        // tiklamis ya da iki yonetici ayni kaydi acmis olabilir. Bilet
        // uretimi tekrarlanmiyor, mevcut durum donuyor.
        if (odeme.IsSuccessful)
        {
            logger.LogInformation(
                "Havale zaten onaylanmis; durum degismedi. OdemeId: {OdemeId}", odeme.Id);

            return Result.Success(new PaymentCompletionResult(
                odeme.Id, odeme.Status, odeme.ReservationId, false, []));
        }

        if (!odeme.IsPending)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotPending);

        logger.LogInformation(
            "Havale onaylaniyor. OdemeId: {OdemeId}, OnaylayanId: {OnaylayanId}",
            odeme.Id,
            currentUser.UserId);

        // Ekstre numarasi verilmediyse basvuru kodu korunuyor; kaydin hicbir
        // referansi olmamasindan iyi.
        var referans = string.IsNullOrWhiteSpace(request.Reference)
            ? odeme.ProviderReference
            : request.Reference.Trim();

        return await finalizer.CloseAsSucceededAsync(
            odeme, referans, clock.UtcNow, cancellationToken);
    }
}

/// <summary>
/// Yonetici havalenin gelmedigini bildirir; rezervasyon iptal olur, koltuklar doner.
/// </summary>
/// <remarks>
/// Suresi dolan havale zaten arka plan isiyle dusuyor. Bu komut o sureden
/// once karar verebilmek icin: para gelmeyecegi belliyse (musteri vazgecti,
/// yanlis tutar gonderdi) koltugu yirmi dort saat bos tutmanin anlami yok.
/// </remarks>
public sealed record RejectBankTransferCommand(Guid PaymentId, string Reason)
    : IRequest<Result<PaymentCompletionResult>>;

internal sealed class RejectBankTransferCommandValidator
    : AbstractValidator<RejectBankTransferCommand>
{
    public RejectBankTransferCommandValidator()
    {
        RuleFor(komut => komut.PaymentId).NotEmpty();

        // Sebep ZORUNLU: koltuklari geri alan ve musteriye bildirim giden bir
        // karar, gerekcesiz kayda gecmemeli.
        RuleFor(komut => komut.Reason)
            .NotEmpty().WithMessage("Ret sebebi zorunlu.")
            .MaximumLength(300);
    }
}

internal sealed class RejectBankTransferCommandHandler(
    IPaymentRepository payments,
    PaymentFinalizer finalizer,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<RejectBankTransferCommandHandler> logger)
    : IRequestHandler<RejectBankTransferCommand, Result<PaymentCompletionResult>>
{
    public async Task<Result<PaymentCompletionResult>> Handle(
        RejectBankTransferCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var odeme = await payments.GetAggregateAsync(request.PaymentId, cancellationToken);

        if (odeme is null)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotFound);

        if (!odeme.IsBankTransfer)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotBankTransfer);

        // BASARILI ODEME REDDEDILEMEZ. Onaylanmis bir havaleyi geri almanin
        // yolu iade akisi: para musteride degil bizde ve koltugu geri almak
        // once parayi geri gondermeyi gerektiriyor.
        if (!odeme.IsPending)
            return Result.Failure<PaymentCompletionResult>(PaymentErrors.NotPending);

        logger.LogInformation(
            "Havale reddediliyor. OdemeId: {OdemeId}, RededenId: {RededenId}",
            odeme.Id,
            currentUser.UserId);

        return await finalizer.CloseAsFailedAsync(
            odeme, request.Reason.Trim(), clock.UtcNow, cancellationToken);
    }
}
