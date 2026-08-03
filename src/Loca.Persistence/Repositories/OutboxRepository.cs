using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class OutboxRepository(LocaDbContext context) : IOutboxRepository
{
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize, CancellationToken cancellationToken = default) =>
        // HIC DENENMEMIS mesajlar. Daha once basarisiz olanlar buraya
        // girmiyor; onlari ayri bir is daha seyrek tetikliyor. Ayni sorguda
        // toplansalardi surekli basarisiz olan bir mesaj her turda yeniden
        // denenip yeni mesajlarin sirasini isgal ederdi.
        await context.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount == 0)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(
        int batchSize, CancellationToken cancellationToken = default) =>
        await context.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount > 0 &&
                message.RetryCount < OutboxMessage.MaxRetryCount)
            // En cok denenen once: hakki tukenmek uzere olan mesaj, siranin
            // sonunda beklerse hic islenemeden olu mektuba duser.
            .OrderByDescending(message => message.RetryCount)
            .ThenBy(message => message.OccurredAtUtc)
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
