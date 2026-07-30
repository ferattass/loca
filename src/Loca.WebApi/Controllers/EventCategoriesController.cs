using Loca.Application.Features.Events.Common;
using Loca.Application.Features.Events.GetEventCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Etkinlik kategorileri — referans verisi.</summary>
/// <remarks>
/// Sehir listesiyle ayni gerekce: serbest metin yerine tablo. "Tiyatro",
/// "tiyatro" ve "Tiyatro " ayri degerler olarak birikirse kategoriye gore
/// filtreleme calismaz.
/// </remarks>
[Route("api/v1/event-categories")]
[Tags("Etkinlik Kategorisi")]
public sealed class EventCategoriesController(ISender sender) : ApiControllerBase
{
    /// <summary>Aktif kategorileri listeler.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<EventCategoryItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetEventCategoriesQuery(), cancellationToken));
}
