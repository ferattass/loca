using Loca.Application.Features.Halls.DeleteHall;
using Loca.Application.Features.Halls.UpdateHall;
using Loca.Application.Features.SeatLayouts.GetSeatLayoutsByHall;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Venues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Mekan")]
public sealed class HallsController(ISender sender) : ApiControllerBase
{
    /// <summary>Salon bilgilerini gunceller.</summary>
    /// <remarks>
    /// Kapasite, planlarda uretilmis koltuk sayisinin altina indirilemez.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateHallRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateHallCommand(id, request.Name, request.Capacity);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Salonu siler (isaretleyerek).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new DeleteHallCommand(id), cancellationToken));

    /// <summary>Salonun oturma planlarini listeler.</summary>
    [HttpGet("{id:guid}/seat-layouts")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatLayouts(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetSeatLayoutsByHallQuery(id), cancellationToken));
}
