using Loca.Application.Common.Interfaces;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Outbox.ProcessOutbox;

/// <summary>
/// Bekleyen outbox mesajlarini gonderir.
/// </summary>
/// <param name="OnlyRetries">
/// <c>true</c> ise yalnizca daha once basarisiz olmus mesajlar islenir.
/// Ilk deneme ile yeniden deneme ayri sikliklarda tetikleniyor.
/// </param>
/// <returns>Islenen mesaj sayisi.</returns>
public sealed record ProcessOutboxCommand(int BatchSize, bool OnlyRetries = false)
    : IRequest<int>;

internal sealed class ProcessOutboxCommandHandler(
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    IOutboxDispatcher dispatcher,
    IDateTimeProvider clock,
    ILogger<ProcessOutboxCommandHandler> logger)
    : IRequestHandler<ProcessOutboxCommand, int>
{
    public async Task<int> Handle(ProcessOutboxCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mesajlar = request.OnlyRetries
            ? await outbox.GetRetryableAsync(request.BatchSize, cancellationToken)
            : await outbox.GetPendingAsync(request.BatchSize, cancellationToken);

        if (mesajlar.Count == 0)
            return 0;

        var islenen = 0;

        foreach (var mesaj in mesajlar)
        {
            try
            {
                await dispatcher.DispatchAsync(mesaj.Type, mesaj.Payload, cancellationToken);
                mesaj.MarkProcessed(clock.UtcNow);
                islenen++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // TEK MESAJ TURU BOZMAZ. Istisna disari birakilsaydi bir
                // bozuk mesaj yuzunden ayni turdaki diger mesajlar da
                // islenmeden kalirdi; her biri kendi hata sayacini tasiyor.
                mesaj.MarkFailed(exception.Message);

                logger.LogWarning(
                    exception,
                    "Outbox mesaji gonderilemedi. MesajId: {MesajId}, Tur: {Tur}, Deneme: {Deneme}",
                    mesaj.Id,
                    mesaj.Type,
                    mesaj.RetryCount);
            }
        }

        // Basarililarin isaretlenmesi ve basarisizlarin sayaclari TEK
        // kaydetmede yaziliyor: tur sonunda durum tutarli kalsin.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Outbox turu tamamlandi. Alinan: {Alinan}, Islenen: {Islenen}, Yeniden: {YenidenMi}",
            mesajlar.Count,
            islenen,
            request.OnlyRetries);

        return islenen;
    }
}

/// <summary>
/// Deneme hakki tukenmis mesajlari raporlar.
/// </summary>
/// <remarks>
/// Bu kayitlar sessizce kaybolmamali: sonsuza kadar denenirse kuyruk
/// tikanir, silinirse hata gorunmez olur. Isaretli birakilip her turda
/// sayisi loglanıyor; Gun 9'da yonetici panosuna baglanacak.
/// </remarks>
public sealed record ReportDeadLetteredOutboxCommand(int BatchSize) : IRequest<int>;

internal sealed class ReportDeadLetteredOutboxCommandHandler(
    IOutboxRepository outbox,
    ILogger<ReportDeadLetteredOutboxCommandHandler> logger)
    : IRequestHandler<ReportDeadLetteredOutboxCommand, int>
{
    public async Task<int> Handle(
        ReportDeadLetteredOutboxCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var oluMektuplar = await outbox.GetDeadLetteredAsync(request.BatchSize, cancellationToken);

        if (oluMektuplar.Count == 0)
            return 0;

        logger.LogError(
            "Deneme hakki tukenmis {Sayi} outbox mesaji var. Turler: {Turler}",
            oluMektuplar.Count,
            string.Join(", ", oluMektuplar.Select(mesaj => mesaj.Type).Distinct()));

        return oluMektuplar.Count;
    }
}
