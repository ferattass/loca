using Loca.Application.Features.Organizers.ApplyForOrganizer;
using Loca.Application.Features.Organizers.Common;
using Loca.Application.Features.Organizers.GetOrganizerApplications;
using Loca.Application.Features.Organizers.ReviewOrganizerApplication;
using Loca.Domain.Enums;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Organizers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Organizator olma basvurulari.</summary>
[Route("api/v1/organizer-applications")]
[Tags("Organizator Basvurusu")]
public sealed class OrganizerApplicationsController(ISender sender) : ApiControllerBase
{
    /// <summary>Organizator olmak icin basvurur.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyForOrganizerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ApplyForOrganizerCommand(
            request.CompanyName,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxNumber,
            request.Website,
            request.DocumentFileId);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Basvurulari listeler (admin kuyrugu).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<IReadOnlyList<OrganizerApplicationItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] OrganizerApplicationStatus? status,
        CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetOrganizerApplicationsQuery(status), cancellationToken));

    /// <summary>
    /// Basvuruyu onaylar veya reddeder.
    /// </summary>
    /// <remarks>
    /// Onayda organizator profili olusturulur ve kullaniciya Organizer rolu
    /// verilir; rol atanmasaydi onay hicbir seyi degistirmezdi.
    /// </remarks>
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new ReviewOrganizerApplicationCommand(id, request.Approve, request.RejectionReason),
            cancellationToken));
    }
}
