using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.StudentVerifications.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.StudentVerifications.ReviewStudentVerification;

public sealed record ReviewStudentVerificationCommand(
    Guid VerificationId,
    bool Approve,
    string? RejectionReason) : IRequest<Result>;

public sealed class ReviewStudentVerificationCommandValidator
    : AbstractValidator<ReviewStudentVerificationCommand>
{
    public ReviewStudentVerificationCommandValidator()
    {
        RuleFor(command => command.VerificationId).NotEmpty();

        RuleFor(command => command.RejectionReason)
            .NotEmpty().WithMessage("Red gerekcesi zorunludur.")
            .MaximumLength(500)
            .When(command => !command.Approve);
    }
}

internal sealed class ReviewStudentVerificationCommandHandler(
    IStudentVerificationRepository verifications,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<ReviewStudentVerificationCommandHandler> logger)
    : IRequestHandler<ReviewStudentVerificationCommand, Result>
{
    public async Task<Result> Handle(
        ReviewStudentVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } adminId)
            return Result.Failure(StudentVerificationErrors.Unauthenticated);

        var kayit = await verifications.GetByIdAsync(request.VerificationId, cancellationToken);

        if (kayit is null)
            return Result.Failure(StudentVerificationErrors.NotFound);

        if (request.Approve)
            kayit.Approve(adminId, clock.UtcNow);
        else
            kayit.Reject(adminId, clock.UtcNow, request.RejectionReason!);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Ogrenci dogrulamasi incelendi. KayitId: {KayitId}, Durum: {Durum}",
            kayit.Id,
            kayit.Status);

        return Result.Success();
    }
}

/// <remarks>
/// Admin kuyrugu. Kimlik numarasi bu listede de doniyor cunku admin belgeyi
/// numarayla karsilastiracak; yetki <c>AdminOnly</c> ile sinirli.
/// </remarks>
public sealed record GetStudentVerificationsQuery(StudentVerificationStatus? Status)
    : IRequest<Result<IReadOnlyList<StudentVerificationDetail>>>;

internal sealed class GetStudentVerificationsQueryHandler(
    IStudentVerificationRepository verifications)
    : IRequestHandler<GetStudentVerificationsQuery, Result<IReadOnlyList<StudentVerificationDetail>>>
{
    public async Task<Result<IReadOnlyList<StudentVerificationDetail>>> Handle(
        GetStudentVerificationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kayitlar = await verifications.GetByStatusAsync(request.Status, cancellationToken);

        var liste = kayitlar
            .Select(GetMyStudentVerification.GetMyStudentVerificationQueryHandler.Map)
            .ToList();

        return Result.Success<IReadOnlyList<StudentVerificationDetail>>(liste);
    }
}
