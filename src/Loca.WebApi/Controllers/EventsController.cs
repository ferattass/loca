using Loca.Application.Common.Models;
using Loca.Application.Features.Events.CancelEvent;
using Loca.Application.Features.Events.Common;
using Loca.Application.Features.Events.CreateEvent;
using Loca.Application.Features.Events.CreateEventSession;
using Loca.Application.Features.Events.DeleteEvent;
using Loca.Application.Features.Events.GetEventById;
using Loca.Application.Features.Events.GetEvents;
using Loca.Application.Features.Events.PublishEvent;
using Loca.Application.Features.Events.SetEventPoster;
using Loca.Application.Features.Events.SubmitEventForApproval;
using Loca.Application.Features.Events.UpdateEvent;
using Loca.Application.Features.TicketTypes.CreateTicketType;
using Loca.Application.Features.TicketTypes.GetTicketTypes;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Etkinlik yonetimi.
/// </summary>
/// <remarks>
/// Yetki uc seviyeli calisiyor:
/// <list type="number">
/// <item>Rol: yazma uclari <c>OrganizerOnly</c>, yayin <c>AdminOnly</c>.</item>
/// <item>Policy: rol kombinasyonlari tek isimde toplaniyor.</item>
/// <item>
/// Kaynak sahipligi: handler icinde kontrol ediliyor. Controller'da
/// yapilamaz cunku kaynagi yuklemek gerekir ve architecture kurali
/// controller'in domain entity'ye bagimli olmasini yasakliyor. Ayni
/// kurali <c>ResourceOwnerAuthorizationHandler</c> de cagiriyor.
/// </item>
/// </list>
/// </remarks>
[Tags("Etkinlik")]
public sealed class EventsController(ISender sender) : ApiControllerBase
{
    /// <summary>Etkinlikleri sayfali listeler.</summary>
    /// <param name="mine">
    /// <c>true</c> ise yalnizca istegi yapan organizatorun etkinlikleri
    /// (taslaklar dahil) doner.
    /// </param>
    /// <param name="categoryId">Verilirse yalnizca o kategoridekiler.</param>
    /// <param name="search">
    /// Baslikta gecen metin, buyuk/kucuk harf duyarsiz. Suzme SUNUCUDA:
    /// istemcide yapilsaydi yalnizca cekilmis sayfa suzulur, katalogun
    /// geri kalani filtrenin disinda kalirdi.
    /// </param>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<EventListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] bool mine = false,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventsQuery(
            mine,
            new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize },
            categoryId,
            search,
            fromUtc,
            toUtc);

        return ToResponse(await sender.Send(query, cancellationToken));
    }

    /// <summary>Etkinlik detayini oturumlari ve bilet turleriyle doner.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<EventDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetEventByIdQuery(id), cancellationToken));

    /// <summary>Yeni etkinlik olusturur. Durum: Draft.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateEventCommand(
            request.CategoryId,
            request.Title,
            request.Description,
            request.CancellationPolicy,
            request.CityId,
            request.VenueId,
            request.HallId,
            request.EventDateUtc,
            request.DurationMinutes,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc,
            request.MinimumAge);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = sonuc.Value }, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Etkinligi gunceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateEventCommand(
            id,
            request.CategoryId,
            request.Title,
            request.Description,
            request.CancellationPolicy,
            request.MinimumAge,
            request.CityId,
            request.VenueId,
            request.HallId,
            request.EventDateUtc,
            request.DurationMinutes,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Etkinligi siler (soft delete). Yayindaki etkinlik silinemez, iptal edilir.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new DeleteEventCommand(id), cancellationToken));

    /// <summary>Afis gorselini baglar veya kaldirir.</summary>
    [HttpPatch("{id:guid}/poster")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPoster(
        Guid id,
        [FromBody] SetEventPosterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new SetEventPosterCommand(id, request.PosterFileId), cancellationToken));
    }

    /// <summary>Etkinligi admin onayina gonderir: Draft → PendingApproval.</summary>
    [HttpPost("{id:guid}/submit-for-approval")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitForApproval(
        Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new SubmitEventForApprovalCommand(id), cancellationToken));

    /// <summary>
    /// Etkinligi yayina alir ve satilabilir koltuklari uretir.
    /// </summary>
    /// <remarks>
    /// <b>AdminOnly.</b> Kilavuzun uc tablosu bu ucu EventOwner'a veriyor
    /// ama sartnamenin durum listesinde <c>PendingApproval</c> var; onay
    /// adimi organizatore birakilirsa o durumun karsiligi kalmaz ve
    /// moderasyon ancak bilet satildiktan sonra devreye girer. Organizatorun
    /// karsiligi <c>submit-for-approval</c> ucu.
    /// </remarks>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<PublishEventResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new PublishEventCommand(id), cancellationToken));

    /// <summary>Etkinligi iptal eder; oturumlari da iptal edilir.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new CancelEventCommand(id, request.Reason), cancellationToken));
    }

    /// <summary>Etkinlige oturum ekler.</summary>
    [HttpPost("{id:guid}/sessions")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSession(
        Guid id,
        [FromBody] CreateEventSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateEventSessionCommand(
            id,
            request.HallId,
            request.SeatLayoutId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Etkinligin bilet turlerini listeler.</summary>
    [HttpGet("{id:guid}/ticket-types")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<TicketTypeItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketTypes(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetTicketTypesQuery(id), cancellationToken));

    /// <summary>Etkinlige bilet turu ekler.</summary>
    [HttpPost("{id:guid}/ticket-types")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTicketType(
        Guid id,
        [FromBody] CreateTicketTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateTicketTypeCommand(
            id,
            request.Name,
            request.Price,
            request.Currency,
            request.Quota,
            request.SalesStartsAtUtc,
            request.SalesEndsAtUtc,
            request.RequiresVerification,
            request.SeatSectionId);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetTicketTypes), new { id }, sonuc.Value)
            : ToResponse(sonuc);
    }
}
