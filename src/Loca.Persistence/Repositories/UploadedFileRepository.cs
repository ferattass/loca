using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class UploadedFileRepository(LocaDbContext context) : IUploadedFileRepository
{
    public Task<UploadedFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.UploadedFiles.FirstOrDefaultAsync(file => file.Id == id, cancellationToken);

    public void Add(UploadedFile file) => context.UploadedFiles.Add(file);
}
