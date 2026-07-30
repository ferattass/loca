using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.Domain.Repositories;

public interface IOrganizerRepository
{
    Task<OrganizerApplication?> GetApplicationAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Kullanicinin bekleyen basvurusu var mi.</summary>
    /// <remarks>
    /// Ayni kisi ust uste basvurdugunda admin kuyrugu ayni sirketin
    /// kopyalariyla dolar. Reddedilmis basvurudan sonra yeniden basvuru
    /// serbest — kural "bekleyen" ile sinirli.
    /// </remarks>
    Task<bool> HasPendingApplicationAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizerApplication>> GetApplicationsAsync(
        OrganizerApplicationStatus? status,
        CancellationToken cancellationToken = default);

    Task<bool> ProfileExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    void AddApplication(OrganizerApplication application);

    void AddProfile(OrganizerProfile profile);
}
