using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Reservations.Common;
using Loca.Domain.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Reservations.CreateReservation;

/// <summary>
/// Secilen koltuklari kullanici adina kilitler ve rezervasyonu acar.
/// </summary>
/// <remarks>
/// <b>Projenin en kritik akisi.</b> Buradaki bir hata "ayni koltuk iki
/// kisiye satildi" demek. Adimlarin sirasi rastgele degil; ozellikle
/// koltuk durumunun transaction icinde YENIDEN dogrulandigi adim
/// atlanirsa katmanli savunma tek katmana iner.
/// </remarks>
/// <param name="IdempotencyKey">
/// Istemcinin urettigi tekil anahtar (<c>Idempotency-Key</c> basligi).
/// Ayni anahtarla gelen ikinci istek yeni rezervasyon acmaz.
/// </param>
public sealed record CreateReservationCommand(
    Guid EventSessionId,
    IReadOnlyList<Guid> EventSeatIds,
    string IdempotencyKey) : IRequest<Result<ReservationDetail>>;

public sealed class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(command => command.EventSessionId)
            .NotEmpty().WithMessage("Oturum zorunludur.");

        RuleFor(command => command.EventSeatIds)
            .NotNull().WithMessage("Koltuk listesi zorunludur.")
            .Must(seatIds => seatIds is { Count: > 0 })
            .WithMessage("En az bir koltuk secilmelidir.");

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key basligi zorunludur.")
            .MaximumLength(100);
    }
}

internal sealed class CreateReservationCommandHandler(
    IReservationRepository reservations,
    IStudentVerificationRepository studentVerifications,
    IReservationQueries queries,
    IUnitOfWork unitOfWork,
    IDistributedLockService locks,
    IReservationPolicy policy,
    IServiceFeeProvider serviceFee,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<CreateReservationCommandHandler> logger)
    : IRequestHandler<CreateReservationCommand, Result<ReservationDetail>>
{
    public async Task<Result<ReservationDetail>> Handle(
        CreateReservationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<ReservationDetail>(ReservationErrors.Unauthenticated);

        // Saat TEK KEZ okunuyor. Her adimda yeniden okunsaydi kontrol ile
        // yazma arasinda milisaniyeler kayar ve "kontrolde gecerliydi ama
        // yazarken suresi dolmustu" gibi izi surulemez durumlar cikardi.
        var utcNow = clock.UtcNow;

        var seatIds = request.EventSeatIds.Distinct().ToList();

        // --- 1 · Idempotency kontrolu --------------------------------------
        var oncekiSonuc = await TryReturnExistingAsync(
            userId, request.IdempotencyKey, utcNow, cancellationToken);

        if (oncekiSonuc is not null)
            return oncekiSonuc;

        // --- 2 · Oturum satisa acik mi -------------------------------------
        var oturumKontrolu = await CheckSessionAsync(request.EventSessionId, utcNow, cancellationToken);

        if (oturumKontrolu is { } oturumHatasi)
            return Result.Failure<ReservationDetail>(oturumHatasi);

        // --- 3 · Kullanicinin bu oturumdaki bilet limiti --------------------
        var mevcutKoltukSayisi = await reservations.CountActiveSeatsAsync(
            userId, request.EventSessionId, utcNow, cancellationToken);

        if (mevcutKoltukSayisi + seatIds.Count > policy.MaxSeatsPerUserPerSession)
        {
            return Result.Failure<ReservationDetail>(
                ReservationErrors.SeatLimitExceeded(
                    policy.MaxSeatsPerUserPerSession, mevcutKoltukSayisi));
        }

        // --- 4 · Redis kilidi ----------------------------------------------
        // Kilit, transaction'dan ONCE ve transaction'i KAPSAYACAK sekilde
        // aliniyor: `await using` bildirimleri ters sirada birakildigi icin
        // once transaction, sonra kilit serbest kalir. Kilit once
        // birakilsaydi commit ile birakma arasindaki bosluga baska bir
        // istek girebilirdi.
        var lockKeys = SeatLockKeys.For(request.EventSessionId, seatIds);

        await using var seatLock = await locks.AcquireAsync(
            lockKeys, policy.LockDuration, cancellationToken);

        if (seatLock is null)
        {
            logger.LogInformation(
                "Koltuklar baskasinda, rezervasyon acilmadi. OturumId: {OturumId}, Koltuk: {KoltukSayisi}",
                request.EventSessionId,
                seatIds.Count);

            return Result.Failure<ReservationDetail>(ReservationErrors.SeatNotAvailable);
        }

        if (seatLock.IsDegraded)
        {
            // Akis durmuyor (sartname: Redis kapaliyken sistem cokmemeli)
            // ama bu satir olmadan, yarisan iki istegin neden veritabanina
            // kadar gittigi sonradan anlasilamaz.
            logger.LogWarning(
                "Rezervasyon Redis kilidi OLMADAN ilerliyor. " +
                "Eszamanlilik yalnizca transaction ve xmin damgasiyla korunuyor. OturumId: {OturumId}",
                request.EventSessionId);
        }

        Guid rezervasyonId;

        // --- 5-9 · Transaction ---------------------------------------------
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            // 5 · KOLTUKLARI YENIDEN DOGRULA. Bu adim asla atlanmaz.
            // Redis kilidini almis olmak koltugun veritabaninda hâlâ bos
            // oldugunu GARANTI ETMEZ: Redis'e ulasilamamis (degraded),
            // yeniden baslamis veya TTL dolmus olabilir. Kabul olcutu
            // "50 paralel istekte 1 basari" bu adim atlandiginda 50/50
            // verir — testin bu adimi kontrol ettigi nokta tam burasi.
            var koltuklar = await reservations.GetSeatsForUpdateAsync(
                request.EventSessionId, seatIds, cancellationToken);

            if (koltuklar.Count != seatIds.Count)
                return Result.Failure<ReservationDetail>(ReservationErrors.SeatNotInSession);

            if (koltuklar.Any(koltuk => !koltuk.IsAvailable(utcNow)))
            {
                logger.LogInformation(
                    "Koltuk veritabaninda musait degil. OturumId: {OturumId}", request.EventSessionId);

                return Result.Failure<ReservationDetail>(ReservationErrors.SeatNotAvailable);
            }

            // Ogrenci dogrulamasi: Gun 5'te kayit ve onay tarafi yazilmisti,
            // satin alma tarafina baglanmasi bugune birakilmisti.
            var dogrulamaHatasi = await CheckStudentVerificationAsync(
                userId, koltuklar, utcNow, cancellationToken);

            if (dogrulamaHatasi is { } belgeHatasi)
                return Result.Failure<ReservationDetail>(belgeHatasi);

            // 6-7 · Rezervasyon ve kilitler.
            // TUTAR SUNUCUDA: fiyat istekten degil koltugun kendi
            // satirindan okunuyor.
            var satirlar = koltuklar
                .Select(koltuk => new ReservationLine(koltuk.Id, koltuk.TicketTypeId, koltuk.Price))
                .ToList();

            var hizmetBedeli = await serviceFee.GetAsync(cancellationToken);

            var rezervasyon = new Reservation(
                userId,
                request.EventSessionId,
                request.IdempotencyKey,
                satirlar,
                utcNow,
                policy.LockDuration,
                policy.MaxSeatsPerUserPerSession,
                // Politika rezervasyon ANINDA okunuyor ve tutar kaydedilirken
                // donduruluyor: yuzde sonradan degistiginde bu rezervasyonun
                // bedeli olduğu gibi kaliyor. Her okumada yeniden
                // hesaplansaydi kullanicinin gordugu tutar ile odedigi tutar
                // farkli olabilirdi.
                hizmetBedeli);

            foreach (var koltuk in koltuklar)
                koltuk.Lock(userId, rezervasyon.Id, utcNow, policy.LockDuration);

            reservations.Add(rezervasyon);

            // 8 · Tek yazma. Koltuk durumlari, rezervasyon ve kalemler ayni
            // transaction'da; biri yazilip digeri yazilamazsa hicbiri yazilmaz.
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException exception)
                when (exception.ConstraintName == DatabaseConstraints.ReservationIdempotencyKey)
            {
                // Ayni anahtarla iki istek AYNI ANDA geldi: 1. adimdaki
                // okuma ikisinde de bos donmustu. Veritabani kisiti yarisi
                // cozuyor; kaybeden taraf kazananin kaydini doner.
                // Transaction geri alinacagi icin okuma disaridan yapiliyor.
                logger.LogInformation(
                    "Idempotency yarisi: ayni anahtarla eszamanli ikinci istek. KullaniciId: {KullaniciId}",
                    userId);

                await transaction.DisposeAsync();

                var kazanan = await TryReturnExistingAsync(
                    userId, request.IdempotencyKey, utcNow, cancellationToken);

                return kazanan ?? Result.Failure<ReservationDetail>(
                    ReservationErrors.SeatTakenConcurrently);
            }
            catch (ConcurrencyConflictException exception)
            {
                // xmin damgasi tutmadi: koltuk okunduktan sonra, yazilmadan
                // once baskasi tarafindan guncellendi. Katmanli savunmanin
                // IKINCI halkasi burada devreye giriyor.
                logger.LogInformation(
                    exception,
                    "Koltuk yaris durumunda kaybedildi. OturumId: {OturumId}", request.EventSessionId);

                return Result.Failure<ReservationDetail>(ReservationErrors.SeatTakenConcurrently);
            }

            // 9 · Commit
            await transaction.CommitAsync(cancellationToken);

            rezervasyonId = rezervasyon.Id;

            logger.LogInformation(
                "Rezervasyon acildi. RezervasyonId: {RezervasyonId}, Koltuk: {KoltukSayisi}, " +
                "Tutar: {Tutar}, Bitis: {Bitis}",
                rezervasyon.Id,
                rezervasyon.SeatCount,
                rezervasyon.TotalAmount,
                rezervasyon.ExpiresAtUtc);
        }

        // 10 · Kilit `await using` ile burada birakiliyor.

        var detay = await queries.GetDetailAsync(rezervasyonId, utcNow, cancellationToken);

        return detay is null
            ? Result.Failure<ReservationDetail>(ReservationErrors.NotFound)
            : Result.Success(detay);
    }

    /// <summary>
    /// Ayni anahtarla daha once acilmis rezervasyon varsa onun detayini doner.
    /// </summary>
    /// <returns>Kayit yoksa <c>null</c>.</returns>
    private async Task<Result<ReservationDetail>?> TryReturnExistingAsync(
        Guid userId, string idempotencyKey, DateTime utcNow, CancellationToken cancellationToken)
    {
        var mevcut = await reservations.GetByIdempotencyKeyAsync(
            userId, idempotencyKey, cancellationToken);

        if (mevcut is null)
            return null;

        logger.LogInformation(
            "Ayni Idempotency-Key ile ikinci istek; yeni rezervasyon acilmadi. " +
            "RezervasyonId: {RezervasyonId}",
            mevcut.Id);

        var detay = await queries.GetDetailAsync(mevcut.Id, utcNow, cancellationToken);

        return detay is null
            ? Result.Failure<ReservationDetail>(ReservationErrors.NotFound)
            : Result.Success(detay);
    }

    /// <returns>Oturum satisa acik degilse sebebi, acikse <c>null</c>.</returns>
    private async Task<Error?> CheckSessionAsync(
        Guid eventSessionId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var oturum = await reservations.GetSessionInfoAsync(eventSessionId, cancellationToken);

        if (oturum is null)
            return ReservationErrors.SessionNotFound;

        if (!oturum.EventIsPublished)
            return ReservationErrors.EventNotPublished;

        if (oturum.SessionIsCancelled)
            return ReservationErrors.SessionCancelled;

        if (!oturum.SeatsGenerated)
            return ReservationErrors.SeatsNotGenerated;

        if (utcNow < oturum.SalesStartsAtUtc)
            return ReservationErrors.SalesNotStarted;

        // Sinir dahil degil: satis bitis anina ULASILDIGINDA satis kapali.
        if (utcNow >= oturum.SalesEndsAtUtc)
            return ReservationErrors.SalesEnded;

        return null;
    }

    /// <summary>
    /// Secilen koltuklardan biri belge isteyen bir bilet turundeyse
    /// kullanicinin gecerli dogrulamasi var mi.
    /// </summary>
    /// <remarks>
    /// Kontrol satin alma anina konuldu, kayit anina degil: belge onaylandiktan
    /// sonra suresi dolabilir. Kayit aninda bakilsaydi gecen donemin belgesiyle
    /// bu donemin ogrenci bileti alinabilirdi.
    /// </remarks>
    private async Task<Error?> CheckStudentVerificationAsync(
        Guid userId,
        IReadOnlyList<EventSeat> koltuklar,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var belgeIsteyen = koltuklar.Any(koltuk => koltuk.TicketType is { RequiresVerification: true });

        if (!belgeIsteyen)
            return null;

        var belge = await studentVerifications.GetByUserIdAsync(userId, cancellationToken);

        if (belge is not null && belge.IsValid(utcNow))
            return null;

        // Belgenin icerigi (kimlik/ogrenci numarasi) LOGLANMIYOR; yalnizca
        // kaydin var olup olmadigi yaziliyor.
        logger.LogInformation(
            "Ogrenci dogrulamasi gerektiren bilet turu icin gecerli belge yok. " +
            "KullaniciId: {KullaniciId}, BelgeVar: {BelgeVar}",
            userId,
            belge is not null);

        return ReservationErrors.StudentVerificationRequired;
    }
}
