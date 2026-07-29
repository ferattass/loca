using Loca.Application.Features.SeatLayouts.ToggleSeatActive;
using Loca.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Mekan")]
public sealed class SeatsController(ISender sender) : ApiControllerBase
{
    /// <summary>Koltugu satisa acar veya kapatir.</summary>
    /// <remarks>
    /// Bozuk koltuk, kolon arkasi veya teknik ekip icin ayrilan koltuk
    /// pasiflestirilir; silinmez.
    /// </remarks>
    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<bool>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new ToggleSeatActiveCommand(id), cancellationToken));
}
