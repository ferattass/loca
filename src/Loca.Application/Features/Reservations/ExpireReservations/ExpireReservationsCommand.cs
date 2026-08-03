using Loca.Application.Common.Interfaces;
using Loca.Domain.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Reservations.ExpireReservations;

/// <summary>
/// Suresi dolmus rezervasyonlari kapatir ve koltuklarini serbest birakir.
/// </summary>
/// <remarks>
/// Bugun basit bir <c>BackgroundService</c> tarafindan cagriliyor; Gun 7'de
/// ayni komutu Hangfire calistiracak. Isin kendisi burada oldugu icin
/// zamanlayici degistiginde is mantigina dokunulmayacak — arka plan islerinin
/// dogrudan zamanlayicinin icine yazilmasi, ayni mantigin elle tetiklenmesini
/// veya test edilmesini imkansiz kilar.
///
/// <para>
/// <b>Kilit suresinin dolmasi tek basina koltugu bosaltmiyor;</b> okuma
/// tarafi suresi gecmis kilidi zaten "bos" gosteriyor (bkz.
/// <c>EventSeat.IsAvailable</c>). Bu is, veritabanindaki durumu da
/// gerceklikle hizaliyor: aksi hâlde raporlar "kilitli" gorunen ama aslinda
/// bos olan koltuklarla sisirdi.
/// </para>
/// </remarks>
/// <param name="BatchSize">Tek turda islenecek en fazla rezervasyon.</param>
public sealed record ExpireReservationsCommand(int BatchSize) : IRequest<int>;

internal sealed class ExpireReservationsCommandHandler(
    IReservationRepository reservations,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<ExpireReservationsCommandHandler> logger)
    : IRequestHandler<ExpireReservationsCommand, int>
{
    public async Task<int> Handle(
        ExpireReservationsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var utcNow = clock.UtcNow;

        var suresiDolanlar = await reservations.GetExpiredAsync(
            utcNow, request.BatchSize, cancellationToken);

        if (suresiDolanlar.Count == 0)
            return 0;

        var kimlikler = suresiDolanlar.Select(reservation => reservation.Id).ToList();

        // Tek sorgu. Rezervasyon basina ayri sorgu calistirilsaydi bir turda
        // yuz gidis donus olurdu.
        var koltuklar = await reservations.GetSeatsOfReservationsAsync(
            kimlikler, cancellationToken);

        foreach (var rezervasyon in suresiDolanlar)
            rezervasyon.Expire();

        foreach (var koltuk in koltuklar)
            koltuk.Release();

        try
        {
            // Tek SaveChanges = tek transaction: rezervasyonlarin kapanmasi
            // ile koltuklarin birakilmasi ya birlikte olur ya hic olmaz.
            // Ayri kaydedilseydi araya giren bir hata, kapanmis bir
            // rezervasyonun koltuklarini kilitli birakabilirdi.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException exception)
        {
            // Kullanici tam bu sirada suresini uzatmis veya iptal etmis
            // olabilir. Tur bosa gider, bir sonraki turda kalan kayitlar
            // yeniden ele alinir — burada firlatmak, arka plan servisini
            // durdurmaktan baska bir ise yaramazdi.
            logger.LogWarning(
                exception,
                "Sure dolumu turu cakisma nedeniyle yazilamadi; sonraki turda tekrar denenecek.");

            return 0;
        }

        logger.LogInformation(
            "Suresi dolan rezervasyonlar kapatildi. Rezervasyon: {RezervasyonSayisi}, " +
            "SerbestKalanKoltuk: {KoltukSayisi}",
            suresiDolanlar.Count,
            koltuklar.Count);

        return suresiDolanlar.Count;
    }
}
