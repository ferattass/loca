using System.Globalization;
using System.Text.Json;
using Loca.Application.Common.Interfaces;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Outbox.ScheduledReports;

/// <summary>
/// Yaklasan etkinlikler icin hatirlatma mesaji kuyruga yazar.
/// </summary>
/// <remarks>
/// <b>Bildirim buradan GONDERILMIYOR</b>, outbox'a yaziliyor. Is dogrudan
/// e-posta gonderseydi SMTP yavasladiginda is uzar, hata verdiginde de
/// hangi kullanicilara gittigi belirsiz kalirdi. Kuyruk, gonderimi ayri bir
/// isin sorumlulugu yapiyor ve tekrar denemeyi mumkun kiliyor.
/// </remarks>
/// <param name="LeadHours">Etkinlige kac saat kala hatirlatilacagi.</param>
/// <param name="WindowHours">
/// Taranan aralik. Isin calisma sikligiyla ayni olmali: daha genis olsaydi
/// ayni rezervasyon iki turda birden bulunur ve iki hatirlatma giderdi.
/// </param>
public sealed record SendUpcomingEventRemindersCommand(int LeadHours = 24, int WindowHours = 1)
    : IRequest<int>;

internal sealed class SendUpcomingEventRemindersCommandHandler(
    IReservationRepository reservations,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<SendUpcomingEventRemindersCommandHandler> logger)
    : IRequestHandler<SendUpcomingEventRemindersCommand, int>
{
    public async Task<int> Handle(
        SendUpcomingEventRemindersCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var utcNow = clock.UtcNow;
        var baslangic = utcNow.AddHours(request.LeadHours);
        var bitis = baslangic.AddHours(request.WindowHours);

        var yaklasanlar = await reservations.GetUpcomingForReminderAsync(
            baslangic, bitis, cancellationToken);

        if (yaklasanlar.Count == 0)
            return 0;

        foreach (var kayit in yaklasanlar)
        {
            outbox.Add(new OutboxMessage(
                "EventReminder",
                JsonSerializer.Serialize(new
                {
                    reservationId = kayit.ReservationId,
                    userId = kayit.UserId,
                    eventTitle = kayit.EventTitle,
                    venueName = kayit.VenueName,
                    startsAtUtc = kayit.StartsAtUtc,
                    seatCount = kayit.SeatCount,
                }),
                utcNow,
                kayit.ReservationId));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Yaklasan etkinlik hatirlatmasi kuyruga yazildi. Rezervasyon: {Sayi}, " +
            "Aralik: {Baslangic} - {Bitis}",
            yaklasanlar.Count,
            baslangic,
            bitis);

        return yaklasanlar.Count;
    }
}

/// <summary>
/// Bir onceki gunun satis ozetini kuyruga yazar.
/// </summary>
/// <remarks>
/// Ozet DUNU kapsiyor, bugunu degil: is gece calistiginda "bugun" henuz
/// tamamlanmamis olur ve rapor her gun eksik cikardi.
/// </remarks>
public sealed record WriteDailySalesSummaryCommand : IRequest<int>;

internal sealed class WriteDailySalesSummaryCommandHandler(
    IPaymentRepository payments,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<WriteDailySalesSummaryCommandHandler> logger)
    : IRequestHandler<WriteDailySalesSummaryCommand, int>
{
    public async Task<int> Handle(
        WriteDailySalesSummaryCommand request, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var bugun = utcNow.Date;
        var dun = bugun.AddDays(-1);

        var ozet = await payments.GetDailySummaryAsync(dun, bugun, cancellationToken);

        outbox.Add(new OutboxMessage(
            "DailySalesSummary",
            JsonSerializer.Serialize(new
            {
                date = dun.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                succeededCount = ozet.SucceededCount,
                totalAmount = ozet.TotalAmount,
                refundedCount = ozet.RefundedCount,
                refundedAmount = ozet.RefundedAmount,
                failedCount = ozet.FailedCount,
                currency = ozet.Currency,
            }),
            utcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Gunluk satis ozeti yazildi. Tarih: {Tarih}, Basarili: {Basarili}, " +
            "Tutar: {Tutar} {Birim}, Iade: {Iade}, Basarisiz: {Basarisiz}",
            dun.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ozet.SucceededCount,
            ozet.TotalAmount,
            ozet.Currency,
            ozet.RefundedCount,
            ozet.FailedCount);

        return ozet.SucceededCount;
    }
}
