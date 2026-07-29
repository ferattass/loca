using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Venues.CreateVenue;

public sealed record CreateVenueCommand(
    Guid CityId,
    string Name,
    string Address,
    string? Description,
    string? PhoneNumber) : IRequest<Result<Guid>>;

public sealed class CreateVenueCommandValidator : AbstractValidator<CreateVenueCommand>
{
    public CreateVenueCommandValidator()
    {
        RuleFor(command => command.CityId)
            .NotEmpty().WithMessage("Sehir zorunludur.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Mekan adi zorunludur.")
            .MaximumLength(200).WithMessage("Mekan adi en fazla 200 karakter olabilir.");

        RuleFor(command => command.Address)
            .NotEmpty().WithMessage("Adres zorunludur.")
            .MaximumLength(500).WithMessage("Adres en fazla 500 karakter olabilir.");

        RuleFor(command => command.Description)
            .MaximumLength(2000).WithMessage("Aciklama en fazla 2000 karakter olabilir.");

        RuleFor(command => command.PhoneNumber)
            .MaximumLength(20).WithMessage("Telefon numarasi en fazla 20 karakter olabilir.")
            .Matches(@"^[0-9+()\s-]+$").WithMessage("Telefon numarasi gecersiz.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
    }
}

internal sealed class CreateVenueCommandHandler(
    IVenueRepository venues,
    IUnitOfWork unitOfWork,
    ILogger<CreateVenueCommandHandler> logger)
    : IRequestHandler<CreateVenueCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateVenueCommand request, CancellationToken cancellationToken)
    {
        if (!await venues.CityExistsAsync(request.CityId, cancellationToken))
            return Result.Failure<Guid>(VenueErrors.CityNotFound);

        if (await venues.NameExistsAsync(request.CityId, request.Name, null, cancellationToken))
            return Result.Failure<Guid>(VenueErrors.NameAlreadyExists);

        var venue = new Venue(request.CityId, request.Name, request.Address, request.Description);
        venue.Update(request.Name, request.Address, request.Description, request.PhoneNumber);

        venues.Add(venue);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Mekan olusturuldu. MekanId: {MekanId}", venue.Id);

        return Result.Success(venue.Id);
    }
}
