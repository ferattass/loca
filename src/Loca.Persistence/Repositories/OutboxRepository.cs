using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class OutboxRepository(LocaDbContext context) : IOutboxRepository
{
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize, CancellationToken cancellationToken = default) =>
        // Olu mektup (deneme hakki tukenmis) kayitlar bilerek DISARIDA
        // birakiliyor: donselerdi is her turda ayni basarisiz mesaji
        // yeniden dener ve kuyrugun geri kalani hic islenmezdi.
        await context.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount < OutboxMessage.MaxRetryCount)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize, CancellationToken cancellationToken = default) =>
        await context.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount >= OutboxMessage.MaxRetryCount)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(OutboxMessage message) => context.OutboxMessages.Add(message);
}
