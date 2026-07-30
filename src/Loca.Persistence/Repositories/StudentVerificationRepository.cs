using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using Loca.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Repositories;

internal sealed class StudentVerificationRepository(LocaDbContext context)
    : IStudentVerificationRepository
{
    public Task<StudentVerification?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        context.StudentVerifications.FirstOrDefaultAsync(
            verification => verification.Id == id, cancellationToken);

    public Task<StudentVerification?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.StudentVerifications.FirstOrDefaultAsync(
            verification => verification.UserId == userId, cancellationToken);

    public Task<bool> StudentNumberTakenAsync(
        string institutionName,
        string studentNumber,
        Guid? haricId = null,
        CancellationToken cancellationToken = default)
    {
        // ILIKE ve kacis yardimcisi Gun 4'teki gerekceyle: ToLower() hem
        // index'i kullanilamaz kilar hem de Turkce kulturde buyuk I'yi
        // noktasiz i'ye cevirip beklenmeyen esitsizlik uretir.
        var okulKalibi = LikePattern.Escape(institutionName.Trim());
        var numaraKalibi = LikePattern.Escape(studentNumber.Trim());

        return context.StudentVerifications.AnyAsync(
            verification =>
                EF.Functions.ILike(verification.InstitutionName, okulKalibi) &&
                EF.Functions.ILike(verification.StudentNumber, numaraKalibi) &&
                (haricId == null || verification.Id != haricId),
            cancellationToken);
    }

    public Task<bool> NationalIdentityTakenAsync(
        string nationalIdentityNumber,
        Guid? haricId = null,
        CancellationToken cancellationToken = default) =>
        context.StudentVerifications.AnyAsync(
            verification =>
                verification.NationalIdentityNumber == nationalIdentityNumber &&
                (haricId == null || verification.Id != haricId),
            cancellationToken);

    public async Task<IReadOnlyList<StudentVerification>> GetByStatusAsync(
        StudentVerificationStatus? status, CancellationToken cancellationToken = default)
    {
        var sorgu = context.StudentVerifications
            .Include(verification => verification.User)
            .AsNoTracking()
            .AsQueryable();

        if (status is { } durum)
            sorgu = sorgu.Where(verification => verification.Status == durum);

        return await sorgu
            .OrderBy(verification => verification.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(StudentVerification verification) =>
        context.StudentVerifications.Add(verification);
}
