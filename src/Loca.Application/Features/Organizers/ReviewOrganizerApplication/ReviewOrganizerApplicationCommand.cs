using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Organizers.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Organizers.ReviewOrganizerApplication;

/// <summary>
/// Admin karari: basvuruyu onaylar veya reddeder.
/// </summary>
/// <remarks>
/// Onay ve red tek komutta: ikisi de ayni kaydin ayni alanlarini degistiren
/// tek bir karar. Iki ayri komut olsaydi "kim, ne zaman, hangi gerekceyle"
/// mantigi iki yerde tekrar ederdi.
/// </remarks>
public sealed record ReviewOrganizerApplicationCommand(
    Guid ApplicationId,
    bool Approve,
    string? RejectionReason) : IRequest<Result>;

public sealed class ReviewOrganizerApplicationCommandValidator
    : AbstractValidator<ReviewOrganizerApplicationCommand>
{
    public ReviewOrganizerApplicationCommandValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();

        RuleFor(command => command.RejectionReason)
            .NotEmpty().WithMessage("Red gerekcesi zorunludur.")
            .MaximumLength(500)
            .When(command => !command.Approve);
    }
}

internal sealed class ReviewOrganizerApplicationCommandHandler(
    IOrganizerRepository organizers,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    ILogger<ReviewOrganizerApplicationCommandHandler> logger)
    : IRequestHandler<ReviewOrganizerApplicationCommand, Result>
{
    public async Task<Result> Handle(
        ReviewOrganizerApplicationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } adminId)
            return Result.Failure(OrganizerErrors.Unauthenticated);

        var application = await organizers.GetApplicationAsync(
            request.ApplicationId, cancellationToken);

        if (application is null)
            return Result.Failure(OrganizerErrors.ApplicationNotFound);

        if (!request.Approve)
        {
            // Durum kontrolu domain'de: yalnizca bekleyen basvuru reddedilir.
            application.Reject(adminId, clock.UtcNow, request.RejectionReason!);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Organizator basvurusu reddedildi. BasvuruId: {BasvuruId}", application.Id);

            return Result.Success();
        }

        var user = await users.GetByIdAsync(application.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(Error.NotFound(
                "OrganizerApplication.UserNotFound", "Basvuru sahibi kullanici bulunamadi."));
        }

        var role = await users.GetRoleByNameAsync(RoleNames.Organizer, cancellationToken);

        if (role is null)
            return Result.Failure(OrganizerErrors.RoleMissing);

        // Profil basvurunun kendisi tarafindan uretiliyor: "onaylandi ama
        // profil olusmadi" gibi tutarsiz bir ikili ancak bu iki isi
        // ayirmakla ortaya cikar.
        var profile = application.Approve(adminId, clock.UtcNow);

        if (!await organizers.ProfileExistsAsync(application.UserId, cancellationToken))
            organizers.AddProfile(profile);

        // Rol atamasi olmadan onay hicbir sey degistirmezdi: kullanici
        // OrganizerOnly korumali uclara hâlâ giremezdi. AssignRole ayni
        // rolu iki kez eklemiyor.
        user.AssignRole(role);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Organizator basvurusu onaylandi. BasvuruId: {BasvuruId}, KullaniciId: {KullaniciId}",
            application.Id,
            application.UserId);

        return Result.Success();
    }
}
