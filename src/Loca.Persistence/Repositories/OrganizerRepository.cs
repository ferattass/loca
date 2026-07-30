using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class OrganizerRepository(LocaDbContext context) : IOrganizerRepository
{
    public Task<OrganizerApplication?> GetApplicationAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        context.OrganizerApplications.FirstOrDefaultAsync(
            application => application.Id == id, cancellationToken);

    public Task<bool> HasPendingApplicationAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.OrganizerApplications.AnyAsync(
            application =>
                application.UserId == userId &&
                application.Status == OrganizerApplicationStatus.Pending,
            cancellationToken);

    public async Task<IReadOnlyList<OrganizerApplication>> GetApplicationsAsync(
        OrganizerApplicationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var sorgu = context.OrganizerApplications
            .Include(application => application.User)
            .AsNoTracking()
            .AsQueryable();

        if (status is { } durum)
            sorgu = sorgu.Where(application => application.Status == durum);

        // En eski basvuru ustte: admin kuyrugunda sira beklemek adil olsun.
        return await sorgu
            .OrderBy(application => application.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ProfileExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.OrganizerProfiles.AnyAsync(profile => profile.UserId == userId, cancellationToken);

    public void AddApplication(OrganizerApplication application) =>
        context.OrganizerApplications.Add(application);

    public void AddProfile(OrganizerProfile profile) => context.OrganizerProfiles.Add(profile);
}
