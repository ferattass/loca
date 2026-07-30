using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.Domain.Repositories;

public interface IStudentVerificationRepository
{
    Task<StudentVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Kullanicinin dogrulama kaydi. Kisi basina bir kayit tutuluyor.</summary>
    Task<StudentVerification?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu okul ve ogrenci numarasi baska bir kullaniciya kayitli mi.
    /// </summary>
    /// <remarks>
    /// Tekillik kimlik numarasi uzerinden DEGIL (okul, ogrenci no) ikilisi
    /// uzerinden kuruluyor: kimlik numarasi her ogrencide bulunmuyor.
    /// </remarks>
    Task<bool> StudentNumberTakenAsync(
        string institutionName,
        string studentNumber,
        Guid? haricId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kimlik numarasi baska bir kullaniciya kayitli mi. Numara verilmemisse
    /// cagrilmaz — bos deger tekillik kontrolune girmez.
    /// </summary>
    Task<bool> NationalIdentityTakenAsync(
        string nationalIdentityNumber,
        Guid? haricId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentVerification>> GetByStatusAsync(
        StudentVerificationStatus? status, CancellationToken cancellationToken = default);

    void Add(StudentVerification verification);
}
