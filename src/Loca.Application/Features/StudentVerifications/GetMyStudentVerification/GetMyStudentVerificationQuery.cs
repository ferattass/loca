using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.StudentVerifications.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.StudentVerifications.GetMyStudentVerification;

public sealed record GetMyStudentVerificationQuery : IRequest<Result<StudentVerificationDetail>>;

internal sealed class GetMyStudentVerificationQueryHandler(
    IStudentVerificationRepository verifications,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyStudentVerificationQuery, Result<StudentVerificationDetail>>
{
    public async Task<Result<StudentVerificationDetail>> Handle(
        GetMyStudentVerificationQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<StudentVerificationDetail>(StudentVerificationErrors.Unauthenticated);

        var kayit = await verifications.GetByUserIdAsync(userId, cancellationToken);

        return kayit is null
            ? Result.Failure<StudentVerificationDetail>(StudentVerificationErrors.NotFound)
            : Result.Success(Map(kayit));
    }

    internal static StudentVerificationDetail Map(StudentVerification kayit) => new(
        kayit.Id,
        kayit.UserId,
        kayit.FullName,
        kayit.InstitutionName,
        kayit.StudentNumber,
        kayit.NationalIdentityNumber,
        kayit.Identifier,
        kayit.IdentifiedByStudentNumber,
        kayit.DocumentFileId,
        kayit.ValidUntilUtc,
        kayit.Status,
        kayit.ReviewedAt,
        kayit.RejectionReason);
}
