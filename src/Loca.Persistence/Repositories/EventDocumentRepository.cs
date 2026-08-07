using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class EventDocumentRepository(LocaDbContext context) : IEventDocumentRepository
{
    public void Add(EventDocument document) => context.EventDocuments.Add(document);

    public void Remove(EventDocument document) => context.EventDocuments.Remove(document);

    public Task<EventDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.EventDocuments.FirstOrDefaultAsync(
            document => document.Id == id, cancellationToken);
}
