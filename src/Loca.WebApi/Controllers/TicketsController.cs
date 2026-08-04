using Loca.Application.Features.Tickets.CheckInTicket;
using Loca.Application.Features.Tickets.Common;
using Loca.Application.Features.Tickets.GetMyTickets;
using Loca.Application.Features.Tickets.GetTicketById;
using Loca.WebApi.Contracts.Tickets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Kesilmis biletler.</summary>
/// <remarks>
/// Bilet listesi yalnizca token sahibinin biletlerini doner; kullanici
/// kimligi alan olarak kabul EDILMIYOR. Bir biletin QR kodu kapida gecerli
/// olan seydir, dolayisiyla bu liste sizarsa bedava girise donusur.
/// </remarks>
[Route("api/v1/tickets")]
[Tags("Bilet")]
[Authorize]
public sealed class TicketsController(ISender sender) : ApiControllerBase
{
    /// <summary>Kullanicinin biletleri.</summary>
    /// <param name="reservationId">
    /// Verilirse yalnizca o rezervasyonun biletleri doner.
    /// </param>
    /// <remarks>
    /// Yaklasan etkinlikler once, en yakin tarihli en ustte; gecmis
    /// etkinlikler altta ve en yeniden eskiye. Kullanicinin biletine bakma
    /// sebebi neredeyse her zaman "birazdan gidecegim" oldugu icin.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(
        [FromQuery] Guid? reservationId, CancellationToken cancellationToken)
    {
        var sonuc = await sender.Send(new GetMyTicketsQuery(reservationId), cancellationToken);

        return ToResponse(sonuc);
    }

    /// <summary>Tek bir bilet.</summary>
    /// <remarks>
    /// Baskasinin bileti sorulursa 403 degil 404 doner: 403, sorulan
    /// kimlikte gercekten bir bilet oldugunu dogrulardi.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var sonuc = await sender.Send(new GetTicketByIdQuery(id), cancellationToken);

        return ToResponse(sonuc);
    }

    /// <summary>Kapida QR okutur ve gecerliyse bileti kullanilmis isaretler.</summary>
    /// <remarks>
    /// Yalnizca etkinligin organizatoru veya admin okutabilir.
    ///
    /// <para>
    /// <b>Reddedilen bilet de 200 doner.</b> "Bu bilet 20:14'te kullanilmis"
    /// bir sunucu hatasi degil, gorevlinin ekranda gormesi gereken sonucun
    /// kendisi. 404 yalnizca kod hicbir bilete karsilik gelmiyorsa doner.
    /// Karar <c>verdict</c> alanina gore verilmeli.
    /// </para>
    /// </remarks>
    [HttpPost("check-in")]
    [ProducesResponseType<TicketCheckInResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInTicketRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sonuc = await sender.Send(
            new CheckInTicketCommand(request.QrCode), cancellationToken);

        return ToResponse(sonuc);
    }
}
