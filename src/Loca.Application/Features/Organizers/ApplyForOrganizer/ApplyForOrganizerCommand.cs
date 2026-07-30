using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Organizers.Common;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Organizers.ApplyForOrganizer;

/// <summary>
/// Organizator olma basvurusu.
/// </summary>
/// <remarks>
/// Sartname admin yetkileri arasinda "organizator basvurularini
/// onaylayabilir" diyor. Kullanici kendi kendine Organizer rolu alamaz;
/// alabilseydi kayit olan herkes etkinlik yayinlayabilir ve moderasyon
/// zinciri bastan delinirdi.
/// </remarks>
public sealed record ApplyForOrganizerCommand(
    string CompanyName,
    string ContactEmail,
    string ContactPhone,
    string? TaxNumber,
    string? Website,
    Guid? DocumentFileId) : IRequest<Result<Guid>>;

public sealed class ApplyForOrganizerCommandValidator : AbstractValidator<ApplyForOrganizerCommand>
{
    public ApplyForOrganizerCommandValidator()
    {
        RuleFor(command => command.CompanyName)
            .NotEmpty().WithMessage("Sirket adi zorunludur.")
            .MaximumLength(200).WithMessage("Sirket adi en fazla 200 karakter olabilir.");

        RuleFor(command => command.ContactEmail)
            .NotEmpty().WithMessage("Iletisim e-postasi zorunludur.")
            .EmailAddress().WithMessage("Iletisim e-postasi gecersiz.")
            .MaximumLength(256);

        RuleFor(command => command.ContactPhone)
            .NotEmpty().WithMessage("Iletisim telefonu zorunludur.")
            .MaximumLength(20)
            .Matches(@"^[0-9+()\s-]+$").WithMessage("Telefon numarasi gecersiz.");

        RuleFor(command => command.TaxNumber)
            .MaximumLength(20).WithMessage("Vergi numarasi en fazla 20 karakter olabilir.");

        RuleFor(command => command.Website)
            .MaximumLength(300).WithMessage("Web adresi en fazla 300 karakter olabilir.");
    }
}

internal sealed class ApplyForOrganizerCommandHandler(
    IOrganizerRepository organizers,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<ApplyForOrganizerCommandHandler> logger)
    : IRequestHandler<ApplyForOrganizerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ApplyForOrganizerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<Guid>(OrganizerErrors.Unauthenticated);

        if (currentUser.IsInRole(RoleNames.Organizer))
            return Result.Failure<Guid>(OrganizerErrors.AlreadyOrganizer);

        if (await organizers.HasPendingApplicationAsync(userId, cancellationToken))
            return Result.Failure<Guid>(OrganizerErrors.PendingApplicationExists);

        var application = new OrganizerApplication(
            userId,
            request.CompanyName,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxNumber,
            request.Website,
            request.DocumentFileId);

        organizers.AddApplication(application);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Organizator basvurusu alindi. BasvuruId: {BasvuruId}, KullaniciId: {KullaniciId}",
            application.Id,
            userId);

        return Result.Success(application.Id);
    }
}
