using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.Halls.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Halls.CreateHall;

public sealed record CreateHallCommand(
    Guid VenueId,
    string Name,
    int Capacity) : IRequest<Result<Guid>>;

public sealed class CreateHallCommandValidator : AbstractValidator<CreateHallCommand>
{
    public CreateHallCommandValidator()
    {
        RuleFor(command => command.VenueId)
            .NotEmpty().WithMessage("Mekan zorunludur.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Salon adi zorunludur.")
            .MaximumLength(150).WithMessage("Salon adi en fazla 150 karakter olabilir.");

        RuleFor(command => command.Capacity)
            .GreaterThan(0).WithMessage("Kapasite sifirdan buyuk olmali.")
            .LessThanOrEqualTo(100_000).WithMessage("Kapasite en fazla 100000 olabilir.");
    }
}

internal sealed class CreateHallCommandHandler(
    IHallRepository halls,
    IVenueRepository venues,
    IUnitOfWork unitOfWork,
    ILogger<CreateHallCommandHandler> logger)
    : IRequestHandler<CreateHallCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateHallCommand request, CancellationToken cancellationToken)
    {
        if (await venues.GetByIdAsync(request.VenueId, cancellationToken) is null)
            return Result.Failure<Guid>(HallErrors.VenueNotFound);

        if (await halls.NameExistsAsync(request.VenueId, request.Name, null, cancellationToken))
            return Result.Failure<Guid>(HallErrors.NameAlreadyExists);

        var hall = new Hall(request.VenueId, request.Name, request.Capacity);

        halls.Add(hall);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Salon olusturuldu. SalonId: {SalonId}", hall.Id);

        return Result.Success(hall.Id);
    }
}
