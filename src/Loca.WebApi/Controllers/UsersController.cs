using Loca.Application.Features.Reservations.Common;
using Loca.Application.Features.Reservations.GetMyReservations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Giris yapmis kullanicinin kendi kayitlari.</summary>
/// <remarks>
/// Yol <c>/users/me/...</c> seklinde: kullanici kimligi yola YAZILMIYOR,
/// token'dan okunuyor. <c>/users/{id}/reservations</c> olsaydi kimligi
/// degistiren herkes baskasinin rezervasyonlarini isteyebilir ve her
/// endpoint'te ayri bir sahiplik kontrolu gerekirdi.
/// </remarks>
[Route("api/v1/users")]
[Tags("Kullanici")]
[Authorize]
public sealed class UsersController(ISender sender) : ApiControllerBase
{
    /// <summary>Kullanicinin rezervasyonlari, yeniden eskiye.</summary>
    [HttpGet("me/reservations")]
    [ProducesResponseType<IReadOnlyList<ReservationListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyReservations(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetMyReservationsQuery(), cancellationToken));
}
