using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.Venues.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Venues.UpdateVenue;

public sealed record UpdateVenueCommand(
    Guid Id,
    string Name,
    string Address,
    string? Description,
    string? PhoneNumber) : IRequest<Result>;

public sealed class UpdateVenueCommandValidator : AbstractValidator<UpdateVenueCommand>
{
    public UpdateVenueCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("Mekan kimligi zorunludur.");

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

internal sealed class UpdateVenueCommandHandler(
    IVenueRepository venues,
    IUnitOfWork unitOfWork,
    ILogger<UpdateVenueCommandHandler> logger)
    : IRequestHandler<UpdateVenueCommand, Result>
{
    public async Task<Result> Handle(UpdateVenueCommand request, CancellationToken cancellationToken)
    {
        var venue = await venues.GetByIdAsync(request.Id, cancellationToken);

        if (venue is null)
            return Result.Failure(VenueErrors.NotFound);

        // Sehir degistirilmiyor: mekan tasinmaz. Sehir yanlis girildiyse
        // kayit silinip yenisi acilir; boylece bagli salonlarin da farkli
        // bir sehre tasinmasi gibi bir durum olusmaz.
        if (await venues.NameExistsAsync(venue.CityId, request.Name, venue.Id, cancellationToken))
            return Result.Failure(VenueErrors.NameAlreadyExists);

        venue.Update(request.Name, request.Address, request.Description, request.PhoneNumber);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Mekan guncellendi. MekanId: {MekanId}", venue.Id);

        return Result.Success();
    }
}
