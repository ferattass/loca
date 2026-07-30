using Loca.Application.Features.TicketTypes.AssignTicketTypeSection;
using Loca.Application.Features.TicketTypes.DeleteTicketType;
using Loca.Application.Features.TicketTypes.UpdateTicketType;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Bilet turu islemleri.
/// </summary>
/// <remarks>
/// Listeleme ve ekleme etkinlik altinda (<c>/events/{id}/ticket-types</c>),
/// tek kayit uzerindeki islemler burada — kilavuzun uc sozlesmesi boyle
/// tanimliyor. Sebep pratik: guncelleme ve silme icin etkinlik kimligine
/// ihtiyac yok, bilet turu kimligi zaten etkinligi isaret ediyor.
/// </remarks>
[Route("api/v1/ticket-types")]
[Tags("Bilet Turu")]
public sealed class TicketTypesController(ISender sender) : ApiControllerBase
{
    /// <summary>Bilet turunu gunceller. Satisi baslamissa fiyat degisikligi denetime yazilir.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTicketTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateTicketTypeCommand(
            id,
            request.Name,
            request.Price,
            request.Currency,
            request.Quota,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>
    /// Bilet turunu satistan kaldirir.
    /// </summary>
    /// <remarks>
    /// Fiziksel silme degil pasifleştirme: uretilmis koltuklar bu ture
    /// yabanci anahtarla bagli.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new DeleteTicketTypeCommand(id), cancellationToken));

    /// <summary>Bilet turunu koltuk bolumune atar.</summary>
    [HttpPost("{id:guid}/assign-section")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignSection(
        Guid id,
        [FromBody] AssignSectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new AssignTicketTypeSectionCommand(id, request.SeatSectionId), cancellationToken));
    }
}
