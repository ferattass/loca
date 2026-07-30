using Loca.Application.Features.Events.Common;
using Loca.Application.Features.EventSessions.GetSeatAvailability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Etkinlik oturumu islemleri.</summary>
[Route("api/v1/event-sessions")]
[Tags("Etkinlik Oturumu")]
public sealed class EventSessionsController(ISender sender) : ApiControllerBase
{
    /// <summary>
    /// Oturumun koltuk durumlarini bolum bolum doner.
    /// </summary>
    /// <remarks>
    /// Gorsel koltuk planinin veri kaynagi. Anonim erisime acik: kullanici
    /// giris yapmadan da salonun doluluk durumunu gorebilmeli. Kilit sahibi
    /// kullanicinin kimligi yanitta YOK; yalnizca "bu kilit bana mi ait"
    /// bilgisi var.
    /// </remarks>
    [HttpGet("{id:guid}/seat-availability")]
    [AllowAnonymous]
    [ProducesResponseType<SeatAvailability>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatAvailability(
        Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetSeatAvailabilityQuery(id), cancellationToken));
}
