using System.Globalization;
using FluentValidation;
using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Settings;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
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
///
/// <para>
/// Iki yol var: kart odemesi saglayiciya gidiyor, havale hicbir saglayiciya
/// gitmiyor — kayit acilip yoneticinin onayi bekleniyor.
/// </para>
/// </remarks>
public sealed record StartPaymentCommand(
    Guid ReservationId,
    string IdempotencyKey,
    PaymentMethod Method = PaymentMethod.Card)
    : IRequest<Result<PaymentDetail>>;

public sealed class StartPaymentCommandValidator : AbstractValidator<StartPaymentCommand>
{
    public StartPaymentCommandValidator()
    {
        RuleFor(command => command.ReservationId).NotEmpty();

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key basligi zorunludur.")
            .MaximumLength(100);

        RuleFor(command => command.Method)
            .IsInEnum().WithMessage("Gecersiz odeme yontemi.");
    }
}

internal sealed class StartPaymentCommandHandler(
    IPaymentRepository payments,
    IReservationRepository reservations,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPaymentService paymentService,
    IPaymentSettingsReader paymentSettings,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<StartPaymentCommandHandler> logger)
    : IRequestHandler<StartPaymentCommand, Result<PaymentDetail>>
{
    /// <summary>Havale kayitlarinin saglayici adi.</summary>
    /// <remarks>
    /// Calisan saglayicinin adi YAZILMIYOR: havalenin iyzico ile bir ilgisi
    /// yok ve kaydi "Iyzico" diye isaretlemek mutabakatta o saglayiciya ait
    /// gibi gorunmesine yol acardi.
    /// </remarks>
    private const string HavaleSaglayicisi = "BankTransfer";

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

        return request.Method == PaymentMethod.BankTransfer
            ? await HavaleBaslatAsync(rezervasyon, userId, request.IdempotencyKey, utcNow, cancellationToken)
            : await KartBaslatAsync(rezervasyon, userId, request.IdempotencyKey, utcNow, cancellationToken);
    }

    /// <summary>Kart odemesi: kayit acilir, saglayiciya islem yaratilir.</summary>
    private async Task<Result<PaymentDetail>> KartBaslatAsync(
        Reservation rezervasyon,
        Guid userId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var odeme = new Payment(
            rezervasyon.Id,
            userId,
            rezervasyon.TotalAmount,
            paymentService.Name,
            idempotencyKey,
            utcNow,
            PaymentMethod.Card);

        // Saglayiciya once kayit acilip sonra veritabanina yazilmiyor: sira
        // ters olsaydi saglayicida acilmis ama bizde karsiligi olmayan bir
        // islem kalabilirdi. Once kendi kaydimizi yaziyoruz.
        payments.Add(odeme);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Musteri bilgisi saglayiciya GERCEK degerlerle gidiyor. Sabit yer
        // tutucularla gonderilseydi saglayicinin dolandiricilik puanlamasi
        // her islemi ayni kisi sanardi.
        var kullanici = await users.GetByIdAsync(userId, cancellationToken);

        if (kullanici is null)
            return Result.Failure<PaymentDetail>(PaymentErrors.Unauthenticated);

        var sonuc = await paymentService.CreatePaymentAsync(
            new PaymentRequest(
                odeme.Id,
                odeme.Amount.Amount,
                odeme.Amount.Currency,
                new PaymentCustomer(
                    userId, kullanici.FullName, kullanici.Email, currentUser.IpAddress),
                Aciklama(rezervasyon)),
            cancellationToken);

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

        return Result.Success(Detay(odeme, sonuc.RedirectUrl));
    }

    /// <summary>
    /// Havale: kayit acilir, koltuk penceresi banka saatlerine cekilir, onay beklenir.
    /// </summary>
    /// <remarks>
    /// <b>Hicbir saglayiciya gidilmiyor.</b> Paranin geldigini yalnizca banka
    /// hesabina bakan insan gorebilir; onay ucu bu yuzden yoneticide.
    ///
    /// <para>
    /// Kilit penceresi burada uzatiliyor. Uzatilmasaydi kullanici havaleyi
    /// secer, on dakika sonra koltuklari duser ve parayi gonderdiginde
    /// karsiliginda koltuk kalmazdi — iade edilecek bir odeme ve kizgin bir
    /// musteri.
    /// </para>
    /// </remarks>
    private async Task<Result<PaymentDetail>> HavaleBaslatAsync(
        Reservation rezervasyon,
        Guid userId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var ayarlar = await paymentSettings.GetAsync(cancellationToken);
        var havale = ayarlar.BankTransfer;

        // Acik olmasi yetmiyor, banka bilgisi de dolu olmali: "havale ile
        // ode" deyip nereye odeyecegini soylememek en bastan kapali olmaktan
        // kotu.
        if (!havale.Enabled
            || string.IsNullOrWhiteSpace(havale.Iban)
            || string.IsNullOrWhiteSpace(havale.BankName))
        {
            return Result.Failure<PaymentDetail>(PaymentErrors.BankTransferDisabled);
        }

        var odeme = new Payment(
            rezervasyon.Id,
            userId,
            rezervasyon.TotalAmount,
            HavaleSaglayicisi,
            idempotencyKey,
            utcNow,
            PaymentMethod.BankTransfer);

        // Aciklama kodu odeme kimliginden turetiliyor: kullanici bunu havale
        // aciklamasina yaziyor, yonetici gelen ekstreyi bununla esliyor. Ayri
        // bir sayac tutulmadi — kimlik zaten tekil ve kod ondan uretildigi
        // icin iki kaydin ayni kodu almasi mumkun degil.
        odeme.AttachProviderReference(HavaleKodu(odeme.Id));

        payments.Add(odeme);

        // Rezervasyon ve koltuklar AYNI ana kadar uzatiliyor. Ikisi ayri
        // hesaplansaydi biri once duser, rezervasyon koltuksuz ya da koltuk
        // rezervasyonsuz kalirdi.
        var pencere = TimeSpan.FromHours(havale.DeadlineHours);
        rezervasyon.ExtendForBankTransfer(utcNow, pencere);

        var koltuklar = await reservations.GetSeatsOfReservationAsync(
            rezervasyon.Id, cancellationToken);

        foreach (var koltuk in koltuklar)
            koltuk.ExtendLockUntil(utcNow, rezervasyon.ExpiresAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Havale odemesi baslatildi. OdemeId: {OdemeId}, RezervasyonId: {RezervasyonId}, "
                + "Tutar: {Tutar}, Son odeme: {SonOdeme}",
            odeme.Id,
            rezervasyon.Id,
            odeme.Amount,
            rezervasyon.ExpiresAtUtc);

        return Result.Success(Detay(odeme, null));
    }

    /// <summary>Havale aciklamasina yazilacak kod.</summary>
    private static string HavaleKodu(Guid odemeId) =>
        "LOCA-" + odemeId.ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();

    /// <summary>
    /// Saglayicinin sepetinde gorunecek metin.
    /// </summary>
    /// <remarks>
    /// Kart ekstresinde ve saglayici panelinde bu yaziyor. "Rezervasyon"
    /// gibi genel bir metin yazilsaydi kullanici ekstresine bakip hangi
    /// etkinlige odeme yaptigini cikaramaz, bankaya itiraz ederdi.
    /// </remarks>
    private static string Aciklama(Reservation rezervasyon) =>
        rezervasyon.EventSession?.Event?.Title is { Length: > 0 } baslik
            ? baslik
            : "Etkinlik bileti";

    /// <param name="yonlendirme">
    /// Saglayicinin odeme sayfasi. Taklit saglayicida ve havalede yok;
    /// arayuz o zaman kendi ekranini gosteriyor.
    /// </param>
    private static PaymentDetail Detay(Payment odeme, string? yonlendirme) =>
        new(
            odeme.Id,
            odeme.ReservationId,
            odeme.Status,
            odeme.Provider,
            odeme.Method,
            odeme.ProviderReference,
            yonlendirme,
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
