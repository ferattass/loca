using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.SeatLayouts.CreateSeatLayout;

public sealed record CreateSeatLayoutCommand(
    Guid HallId,
    string Name,
    string? Description) : IRequest<Result<Guid>>;

public sealed class CreateSeatLayoutCommandValidator : AbstractValidator<CreateSeatLayoutCommand>
{
    public CreateSeatLayoutCommandValidator()
    {
        RuleFor(command => command.HallId)
            .NotEmpty().WithMessage("Salon zorunludur.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Plan adi zorunludur.")
            .MaximumLength(150).WithMessage("Plan adi en fazla 150 karakter olabilir.");

        RuleFor(command => command.Description)
            .MaximumLength(1000).WithMessage("Aciklama en fazla 1000 karakter olabilir.");
    }
}

internal sealed class CreateSeatLayoutCommandHandler(
    ISeatLayoutRepository seatLayouts,
    IHallRepository halls,
    IUnitOfWork unitOfWork,
    ILogger<CreateSeatLayoutCommandHandler> logger)
    : IRequestHandler<CreateSeatLayoutCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSeatLayoutCommand request, CancellationToken cancellationToken)
    {
        if (await halls.GetByIdAsync(request.HallId, cancellationToken) is null)
            return Result.Failure<Guid>(SeatLayoutErrors.HallNotFound);

        // Yol haritasi Gun 4: ayni salonda ayni isimde iki plan olamaz.
        // Uygulamadaki kontrol anlamli mesaj icin; gercek koruma
        // UNIQUE(HallId, Name) index'i.
        if (await seatLayouts.NameExistsAsync(request.HallId, request.Name, null, cancellationToken))
            return Result.Failure<Guid>(SeatLayoutErrors.NameAlreadyExists);

        var layout = new SeatLayout(request.HallId, request.Name, request.Description);

        seatLayouts.Add(layout);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Oturma plani olusturuldu. PlanId: {PlanId}", layout.Id);

        return Result.Success(layout.Id);
    }
}
