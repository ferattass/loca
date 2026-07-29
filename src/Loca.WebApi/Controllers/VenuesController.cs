using Loca.Application.Common.Models;
using Loca.Application.Features.Halls.CreateHall;
using Loca.Application.Features.Halls.GetHallsByVenue;
using Loca.Application.Features.Venues.Common;
using Loca.Application.Features.Venues.CreateVenue;
using Loca.Application.Features.Venues.DeleteVenue;
using Loca.Application.Features.Venues.GetVenueById;
using Loca.Application.Features.Venues.GetVenues;
using Loca.Application.Features.Venues.SetVenueImage;
using Loca.Application.Features.Venues.UpdateVenue;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Venues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Mekan yonetimi.
/// </summary>
/// <remarks>
/// Listeleme ve detay herkese acik: etkinlik arayan kullanici mekani
/// gorebilmeli. Degistiren uclar yalnizca admin — mekan ve salon verisi
/// sistemin referans verisi, organizator bile degistiremez.
/// </remarks>
[Tags("Mekan")]
public sealed class VenuesController(ISender sender) : ApiControllerBase
{
    /// <summary>Mekanlari sayfali listeler.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<VenueListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? cityId,
        [FromQuery] string? arama,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVenuesQuery(
            cityId,
            arama,
            new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize });

        return ToResponse(await sender.Send(query, cancellationToken));
    }

    /// <summary>Tek mekani salonlariyla birlikte doner.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<VenueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetVenueByIdQuery(id), cancellationToken));

    /// <summary>Yeni mekan olusturur.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVenueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateVenueCommand(
            request.CityId, request.Name, request.Address, request.Description, request.PhoneNumber);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = sonuc.Value }, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Mekan bilgilerini gunceller. Sehir degistirilemez.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateVenueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateVenueCommand(
            id, request.Name, request.Address, request.Description, request.PhoneNumber);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Mekani siler (isaretleyerek).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new DeleteVenueCommand(id), cancellationToken));

    /// <summary>Mekanin kapak gorselini baglar veya kaldirir.</summary>
    /// <remarks>Gorsel once <c>POST /api/v1/files</c> ile yuklenir.</remarks>
    [HttpPatch("{id:guid}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetImage(
        Guid id, [FromBody] SetVenueImageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new SetVenueImageCommand(id, request.ImageFileId);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Mekanin salonlarini listeler.</summary>
    [HttpGet("{id:guid}/halls")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHalls(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetHallsByVenueQuery(id), cancellationToken));

    /// <summary>Mekana salon ekler.</summary>
    [HttpPost("{id:guid}/halls")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHall(
        Guid id, [FromBody] CreateHallRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateHallCommand(id, request.Name, request.Capacity);
        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? CreatedAtAction(nameof(GetHalls), new { id }, sonuc.Value)
            : ToResponse(sonuc);
    }
}
