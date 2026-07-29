using Loca.Application.Features.SeatLayouts.Common;
using Loca.Application.Features.SeatLayouts.CreateSeatLayout;
using Loca.Application.Features.SeatLayouts.CreateSeatSection;
using Loca.Application.Features.SeatLayouts.DeleteSeatLayout;
using Loca.Application.Features.SeatLayouts.GenerateSeats;
using Loca.Application.Features.SeatLayouts.GetSeatLayoutById;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Venues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Mekan")]
[Route("api/v1/seat-layouts")]
public sealed class SeatLayoutsController(ISender sender) : ApiControllerBase
{
    /// <summary>Oturma planini bolumleriyle doner.</summary>
    /// <param name="koltuklarlaBirlikte">
    /// Gorsel plan cizilecekse true. Varsayilan false: 600 koltuklu bir plan
    /// her liste isteginde gereksiz yere tasinmasin.
    /// </param>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<SeatLayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] bool koltuklarlaBirlikte = false,
        CancellationToken cancellationToken = default) =>
        ToResponse(await sender.Send(
            new GetSeatLayoutByIdQuery(id, koltuklarlaBirlikte), cancellationToken));

    /// <summary>Salona yeni oturma plani ekler.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSeatLayoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateSeatLayoutCommand(request.HallId, request.Name, request.Description);
        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = sonuc.Value }, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Plani siler (isaretleyerek).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new DeleteSeatLayoutCommand(id), cancellationToken));

    /// <summary>Plana bolum ekler: Orta Blok, Sag Balkon, Loca gibi.</summary>
    [HttpPost("{id:guid}/sections")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSection(
        Guid id, [FromBody] CreateSeatSectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateSeatSectionCommand(id, request.Name, request.DisplayOrder);
        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Bir bolum icin koltuklari toplu uretir.</summary>
    /// <remarks>
    /// Tek <c>AddRange</c> ve tek <c>SaveChanges</c> ile calisir. Uretilecek
    /// koltuk sayisi salon kapasitesini asarsa 409 doner.
    /// </remarks>
    [HttpPost("{id:guid}/generate-seats")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<GenerateSeatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateSeats(
        Guid id, [FromBody] GenerateSeatsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new GenerateSeatsCommand(
            id,
            request.SeatSectionId,
            request.RowLabels,
            request.SeatsPerRow,
            request.HorizontalSpacing,
            request.VerticalSpacing,
            request.OriginY);

        return ToResponse(await sender.Send(command, cancellationToken));
    }
}
