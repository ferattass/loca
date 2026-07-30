using Loca.Application.Features.StudentVerifications.Common;
using Loca.Application.Features.StudentVerifications.GetMyStudentVerification;
using Loca.Application.Features.StudentVerifications.ReviewStudentVerification;
using Loca.Application.Features.StudentVerifications.SubmitStudentVerification;
using Loca.Domain.Enums;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Organizers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Ogrenci bileti icin ogrencilik dogrulamasi.
/// </summary>
/// <remarks>
/// Sartname bilet turleri arasinda <c>Student</c> sayiyor ve "ogrenci bileti
/// icin dogrulama alani tasarlanmalidir" diyor. <c>TicketType</c> uzerindeki
/// <c>RequiresVerification</c> bayragi "bu tur belge ister" der; belge bu
/// uclarla giriliyor.
///
/// <para>
/// <b>Kimlik numarasi zorunlu degil:</b> verilmezse ogrenci numarasi esas
/// alinir ve kayit reddedilmez.
/// </para>
/// </remarks>
[Route("api/v1/student-verifications")]
[Tags("Ogrenci Dogrulama")]
public sealed class StudentVerificationsController(ISender sender) : ApiControllerBase
{
    /// <summary>Ogrenci bilgisi girer veya mevcut kaydi gunceller.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitStudentVerificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new SubmitStudentVerificationCommand(
            request.FullName,
            request.InstitutionName,
            request.StudentNumber,
            request.ValidUntilUtc,
            request.NationalIdentityNumber,
            request.DocumentFileId);

        var sonuc = await sender.Send(command, cancellationToken);

        return sonuc.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, sonuc.Value)
            : ToResponse(sonuc);
    }

    /// <summary>Kullanicinin kendi dogrulama kaydini doner.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<StudentVerificationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetMyStudentVerificationQuery(), cancellationToken));

    /// <summary>Dogrulama kayitlarini listeler (admin kuyrugu).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<IReadOnlyList<StudentVerificationDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] StudentVerificationStatus? status,
        CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetStudentVerificationsQuery(status), cancellationToken));

    /// <summary>Dogrulamayi onaylar veya reddeder.</summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewStudentVerificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new ReviewStudentVerificationCommand(id, request.Approve, request.RejectionReason),
            cancellationToken));
    }
}
