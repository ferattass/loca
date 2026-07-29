using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface IUploadedFileRepository
{
    Task<UploadedFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(UploadedFile file);
}
