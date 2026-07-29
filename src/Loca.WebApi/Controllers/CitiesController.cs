using Loca.Application.Features.Cities.GetCities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Mekan")]
public sealed class CitiesController(ISender sender) : ApiControllerBase
{
    /// <summary>Aktif sehirleri doner. Etkinlik listelemesinin ana filtresi.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetCitiesQuery(), cancellationToken));
}
