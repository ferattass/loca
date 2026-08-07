using Loca.Application.Features.Events.Documents;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

internal sealed class EventDocumentQueries(LocaDbContext context) : IEventDocumentQueries
{
    public async Task<IReadOnlyList<EtkinlikBelgesi>> GetByEventAsync(
        Guid eventId, CancellationToken cancellationToken = default) =>
        await context.EventDocuments
            .Where(document => document.EventId == eventId)
            // Sozlesme en uste: onay ekibinin ilk baktigi belge o.
            .OrderBy(document => document.Kind)
            .ThenBy(document => document.CreatedAt)
            .Select(document => new EtkinlikBelgesi(
                document.Id,
                document.Kind,
                document.UploadedFile!.OriginalFileName,
                document.UploadedFileId,
                document.UploadedFile!.SizeInBytes,
                document.Note,
                document.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<bool> HasVenueContractAsync(
        Guid eventId, CancellationToken cancellationToken = default) =>
        context.EventDocuments.AnyAsync(
            document =>
                document.EventId == eventId &&
                document.Kind == EventDocumentKind.VenueContract,
            cancellationToken);
}
