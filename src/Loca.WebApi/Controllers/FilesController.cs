using Loca.Application.Features.Files.UploadFile;
using Loca.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Dosya")]
public sealed class FilesController(ISender sender) : ApiControllerBase
{
    /// <summary>Gorsel yukler ve dosya kaydini olusturur.</summary>
    /// <remarks>
    /// Yalnizca .jpg, .jpeg, .png ve .webp kabul edilir; uzantiya degil
    /// dosyanin ilk baytlarina bakilarak dogrulanir. En fazla 5 MB.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<UploadedFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(IFormFile dosya, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dosya);

        // IFormFile bu katmanda kaliyor; Application bir Stream goruyor,
        // HTTP'yi tanimiyor.
        await using var icerik = dosya.OpenReadStream();

        var command = new UploadFileCommand(
            icerik, dosya.FileName, dosya.ContentType, dosya.Length);

        return ToResponse(await sender.Send(command, cancellationToken));
    }
}
