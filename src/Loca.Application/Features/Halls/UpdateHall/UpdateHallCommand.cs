using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.Halls.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Halls.UpdateHall;

public sealed record UpdateHallCommand(
    Guid Id,
    string Name,
    int Capacity) : IRequest<Result>;

public sealed class UpdateHallCommandValidator : AbstractValidator<UpdateHallCommand>
{
    public UpdateHallCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("Salon kimligi zorunludur.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Salon adi zorunludur.")
            .MaximumLength(150).WithMessage("Salon adi en fazla 150 karakter olabilir.");

        RuleFor(command => command.Capacity)
            .GreaterThan(0).WithMessage("Kapasite sifirdan buyuk olmali.")
            .LessThanOrEqualTo(100_000).WithMessage("Kapasite en fazla 100000 olabilir.");
    }
}

internal sealed class UpdateHallCommandHandler(
    IHallRepository halls,
    ISeatLayoutRepository seatLayouts,
    IUnitOfWork unitOfWork,
    ILogger<UpdateHallCommandHandler> logger)
    : IRequestHandler<UpdateHallCommand, Result>
{
    public async Task<Result> Handle(UpdateHallCommand request, CancellationToken cancellationToken)
    {
        var hall = await halls.GetByIdAsync(request.Id, cancellationToken);

        if (hall is null)
            return Result.Failure(HallErrors.NotFound);

        if (await halls.NameExistsAsync(hall.VenueId, request.Name, hall.Id, cancellationToken))
            return Result.Failure(HallErrors.NameAlreadyExists);

        // Kapasite dusuruluyorsa mevcut planlardaki koltuk sayisiyla
        // karsilastirilir. Kontrol edilmeseydi salon, kapasitesinden fazla
        // satilabilir koltuk tasiyabilirdi — koltuk uretimindeki kapasite
        // kontrolu de bu taraftan delinmis olurdu.
        if (request.Capacity < hall.Capacity)
        {
            var planlar = await seatLayouts.GetByHallIdAsync(hall.Id, cancellationToken);

            foreach (var plan in planlar)
            {
                var koltukSayisi = await seatLayouts.CountSeatsAsync(plan.Id, cancellationToken);

                if (koltukSayisi > request.Capacity)
                    return Result.Failure(HallErrors.CapacityBelowExistingSeats);
            }
        }

        hall.Update(request.Name, request.Capacity);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Salon guncellendi. SalonId: {SalonId}", hall.Id);

        return Result.Success();
    }
}
