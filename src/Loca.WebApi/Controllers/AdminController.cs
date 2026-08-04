using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.ChangeUserRole;
using Loca.Application.Features.Admin.Common;
using Loca.Application.Features.Admin.GetOverview;
using Loca.Application.Features.Admin.GetPayments;
using Loca.Application.Features.Admin.GetQueue;
using Loca.Application.Features.Admin.GetUsers;
using Loca.Domain.Enums;
using Loca.WebApi.Authorization;
using Loca.WebApi.Contracts.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>Yonetim paneli uclari.</summary>
/// <remarks>
/// <b>Tamami <c>AdminOnly</c> policy'siyle korunuyor</b> ve bu kural
/// controller seviyesinde: eylem bazinda yazilsaydi yeni eklenen bir
/// eylemde unutulabilirdi ve unutulan yetki kontrolu hicbir hata
/// uretmeden acikta kalirdi.
///
/// <para>
/// Kullaniciya donuk uclardan AYRI tutuluyor. Ayni ucun bir parametreyle
/// "hepsini getir" moduna gecmesi daha az kod olurdu ama o parametrenin
/// bir gun yanlislikla acilmasi butun odemeleri disari verirdi.
/// </para>
/// </remarks>
[Route("api/v1/admin")]
[Tags("Yonetim")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminController(ISender sender) : ApiControllerBase
{
    /// <summary>Panelin acilis ozeti: gunun satisi, kuyruk ve saglik.</summary>
    [HttpGet("overview")]
    [ProducesResponseType<AdminOzeti>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new GetOverviewQuery(), cancellationToken));

    /// <summary>Filtrelenebilir odeme listesi.</summary>
    /// <param name="search">
    /// Ad, e-posta veya saglayici referansi. Tam GUID yazilirsa odeme ve
    /// rezervasyon kimliginden aranir.
    /// </param>
    [HttpGet("payments")]
    [ProducesResponseType<PagedResult<AdminOdemeSatiri>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Payments(
        [FromQuery] PaymentStatus? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToResponse(await sender.Send(
            new GetPaymentsQuery(
                new AdminOdemeFiltresi(
                    status,
                    search,
                    fromUtc,
                    toUtc,
                    new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize })),
            cancellationToken));

    /// <summary>Filtrelenebilir kullanici listesi.</summary>
    [HttpGet("users")]
    [ProducesResponseType<PagedResult<AdminKullanici>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Users(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToResponse(await sender.Send(
            new GetUsersQuery(
                new AdminKullaniciFiltresi(
                    search,
                    role,
                    new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize })),
            cancellationToken));

    /// <summary>Kullaniciya rol verir veya geri alir.</summary>
    /// <remarks>
    /// Admin kendi admin rolunu alamaz: tek adminli bir sistemde panele
    /// girisi olan hic kimse kalmayabilirdi.
    /// </remarks>
    [HttpPost("users/{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ToResponse(await sender.Send(
            new ChangeUserRoleCommand(id, request.RoleName, request.Grant), cancellationToken));
    }

    /// <summary>Outbox kuyrugundaki mesajlar.</summary>
    /// <param name="durum">
    /// <c>Pending</c>, <c>Retryable</c>, <c>DeadLettered</c> veya
    /// <c>Processed</c>.
    /// </param>
    /// <remarks>
    /// Mesajin GOVDESI donmuyor: yuk e-posta adresi gibi kisisel veri
    /// iceriyor.
    /// </remarks>
    [HttpGet("queue")]
    [ProducesResponseType<IReadOnlyList<KuyrukMesaji>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Queue(
        [FromQuery] string durum = "Pending",
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        ToResponse(await sender.Send(new GetQueueQuery(durum, limit), cancellationToken));

    /// <summary>Deneme hakki tukenmis mesaji kuyruga geri koyar.</summary>
    /// <remarks>
    /// Sebep giderilmeden geri konursa mesaj yeniden tukenir; karari veren
    /// kisi sorunun cozuldugunu bilen kisi olmali. Islem loglaniyor.
    /// </remarks>
    [HttpPost("queue/{id:guid}/requeue")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(new RequeueMessageCommand(id), cancellationToken));
}
