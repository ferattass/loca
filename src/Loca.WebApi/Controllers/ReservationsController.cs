using Loca.Application.Features.Reservations.CancelReservation;
using Loca.Application.Features.Reservations.Common;
using Loca.Application.Features.Reservations.CreateReservation;
using Loca.Application.Features.Reservations.ExtendReservation;
using Loca.Application.Features.Reservations.GetReservationById;
using Loca.WebApi.Contracts.Reservations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Koltuk kilitleme ve rezervasyon islemleri.</summary>
[Route("api/v1/reservations")]
[Tags("Rezervasyon")]
[Authorize]
public sealed class ReservationsController(ISender sender) : ApiControllerBase
{
    /// <summary>
    /// Idempotency anahtarinin tasindigi baslik.
    /// </summary>
    /// <remarks>
    /// Sektor standardi ad kullaniliyor (Stripe, PayPal ve IETF taslagi ayni
    /// adi kullaniyor); ozel bir ad uydurmak istemci kutuphanelerinin hazir
    /// destegini kaybettirirdi.
    /// </remarks>
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Secilen koltuklari kilitler ve rezervasyon acar.
    /// </summary>
    /// <remarks>
    /// Ayni <c>Idempotency-Key</c> ile gonderilen ikinci istek yeni
    /// rezervasyon acmaz, ilkinin sonucunu doner. Ag koptugunda veya kullanici
    /// iki kez tikladiginda ayni koltuklar icin iki kayit olusmamasi buna
    /// bagli.
    ///
    /// <para>
    /// 409 iki farkli sebeple donebilir; ayrim yanittaki <c>code</c>
    /// alanindadir: <c>Reservation.SeatNotAvailable</c> koltugun az once
    /// alindigini, <c>Reservation.SeatLimitExceeded</c> bilet limitinin
    /// asildigini soyler. Istemci ilkinde koltuk planini yenilemeli.
    /// </para>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<ReservationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReservationRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Baslik eksikse bos metin gecilir ve dogrulayici alan bazli bir 400
        // uretir. Controller'da elle 400 donulseydi hata bicimi digerlerinden
        // ayrisirdi.
        var command = new CreateReservationCommand(
            request.EventSessionId,
            request.EventSeatIds ?? [],
            idempotencyKey ?? string.Empty);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>Rezervasyon detayi. Yalnizca sahibi veya admin gorur.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReservationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetReservationByIdQuery(id), cancellationToken));

    /// <summary>
    /// Rezervasyonu iptal eder; koltuklar hemen serbest kalir.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new CancelReservationCommand(id), cancellationToken));

    /// <summary>
    /// Kilit suresini bir kez uzatir (+5 dakika).
    /// </summary>
    /// <remarks>
    /// Ikinci uzatma denemesi 409 doner: hak bir kez. Sinirsiz uzatma, tek
    /// bir kullanicinin salonu kilitleyip satisi durdurmasina izin verirdi.
    /// </remarks>
    [HttpPost("{id:guid}/extend")]
    [ProducesResponseType<ReservationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Extend(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new ExtendReservationCommand(id), cancellationToken));
}
