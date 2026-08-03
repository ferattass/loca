using Loca.Application.Features.Payments.Common;
using Loca.Application.Features.Payments.CompletePayment;
using Loca.Application.Features.Payments.GetPaymentById;
using Loca.Application.Features.Payments.RefundPayment;
using Loca.Application.Features.Payments.StartPayment;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Odeme islemleri.</summary>
/// <remarks>
/// <b>Kart bilgisi bu uclardan GECMIYOR.</b> Kullanici saglayicinin odeme
/// sayfasinda kartini giriyor; sunucu yalnizca islem kimligini ve sonucu
/// goruyor. Sartname kart verisi saklamayi kapsam disi birakiyor ve bu akis
/// o karari yapisal olarak zorunlu kiliyor — saklamamak icin ayrica
/// ugrasmaya gerek yok, veri hic gelmiyor.
/// </remarks>
[Route("api/v1/payments")]
[Tags("Odeme")]
[Authorize]
public sealed class PaymentsController(ISender sender) : ApiControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Rezervasyon icin odeme baslatir.</summary>
    /// <remarks>
    /// Tutar istekte TASINMIYOR; rezervasyonun kendi toplamindan kopyalanir.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<PaymentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        [FromBody] StartPaymentRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new StartPaymentCommand(request.ReservationId, idempotencyKey ?? string.Empty);

        return ToResponse(await sender.Send(command, cancellationToken));
    }

    /// <summary>
    /// Odemenin sonucunu saglayiciya sorar; basariliysa bilet uretir.
    /// </summary>
    /// <remarks>
    /// <b>Idempotent.</b> Ayni cagri ikinci kez geldiginde hicbir sey
    /// degismez; yanittaki <c>stateChanged</c> alani <c>false</c> doner ve
    /// mevcut biletler listelenir. Saglayicilar ayni bildirimi birden fazla
    /// kez gonderdigi icin bu bir kolaylik degil zorunluluk.
    /// </remarks>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<PaymentCompletionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new CompletePaymentCommand(id), cancellationToken));

    /// <summary>Odemeyi iade eder. Biletler iptal olur, koltuklar satisa doner.</summary>
    [HttpPost("{id:guid}/refund")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundPaymentRequest? request,
        CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new RefundPaymentCommand(id, request?.Reason), cancellationToken));

    /// <summary>Odeme detayi ve deneme dokumu. Yalnizca sahibi veya admin gorur.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PaymentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetPaymentByIdQuery(id), cancellationToken));
}
