using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.SeatLayouts.CreateSeatSection;

public sealed record CreateSeatSectionCommand(
    Guid SeatLayoutId,
    string Name,
    int DisplayOrder) : IRequest<Result<Guid>>;

public sealed class CreateSeatSectionCommandValidator : AbstractValidator<CreateSeatSectionCommand>
{
    public CreateSeatSectionCommandValidator()
    {
        RuleFor(command => command.SeatLayoutId)
            .NotEmpty().WithMessage("Oturma plani zorunludur.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Bolum adi zorunludur.")
            .MaximumLength(100).WithMessage("Bolum adi en fazla 100 karakter olabilir.");

        RuleFor(command => command.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Siralama degeri negatif olamaz.");
    }
}

internal sealed class CreateSeatSectionCommandHandler(
    ISeatLayoutRepository seatLayouts,
    IUnitOfWork unitOfWork,
    ILogger<CreateSeatSectionCommandHandler> logger)
    : IRequestHandler<CreateSeatSectionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSeatSectionCommand request, CancellationToken cancellationToken)
    {
        var layout = await seatLayouts.GetByIdAsync(
            request.SeatLayoutId, koltuklarlaBirlikte: false, cancellationToken);

        if (layout is null)
            return Result.Failure<Guid>(SeatLayoutErrors.NotFound);

        if (await seatLayouts.SectionNameExistsAsync(
                layout.Id, request.Name, null, cancellationToken))
        {
            return Result.Failure<Guid>(SeatLayoutErrors.SectionNameAlreadyExists);
        }

        var section = new SeatSection(layout.Id, request.Name, request.DisplayOrder);

        seatLayouts.AddSection(section);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bolum olusturuldu. PlanId: {PlanId}, BolumId: {BolumId}", layout.Id, section.Id);

        return Result.Success(section.Id);
    }
}
