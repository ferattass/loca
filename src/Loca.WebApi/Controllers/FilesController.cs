using Loca.Application.Features.Files.GetFile;
using Loca.Application.Features.Files.UploadFile;
using Loca.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

[Tags("Dosya")]
public sealed class FilesController(ISender sender) : ApiControllerBase
{
    /// <summary>Yuklenen dosyayi servis eder.</summary>
    /// <remarks>
    /// <b>Boyle bir uc yoktu:</b> afis ve mekan gorseli yukleniyor ama
    /// hicbir yerden okunamiyordu, arayuz de gorsel yerine dekoratif bir
    /// blok ciziyordu.
    ///
    /// <para>
    /// Anonim erisilebilir ama <b>her dosya acik degil</b>: kayittaki
    /// <c>IsPublic</c> alanina bakiliyor. Belge (sahne sozlesmesi) kapali
    /// kaydediliyor ve yalnizca yukleyeni ile onay ekibi goruyor. Yetkisiz
    /// istek 403 degil <b>404</b> aliyor — 403, "bu kimlikte bir dosya var
    /// ama goremezsin" demek olur ve var olan belgeler denenerek
    /// ogrenilebilirdi.
    /// </para>
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var sonuc = await sender.Send(new GetFileQuery(id), cancellationToken);

        if (sonuc.IsFailure)
            return ToResponse(sonuc);

        // Onbellek basligi dosyanin GORUNURLUGUNE gore.
        //
        // Afiste "public": icerik hicbir zaman degismiyor (yeni yukleme yeni
        // kimlik uretir), tarayici ve CDN uzun sure saklayabilir.
        //
        // Belgede "private, no-store": tek bir public basligi, araya giren
        // bir vekilin veya CDN'in sahne kira sozlesmesini saklayip yetkisi
        // olmayan bir istege servis etmesi demek olurdu — yetki kontrolu
        // sunucuda dogru calissa bile. Tarayicinin diskine yazilmasi da
        // istenmiyor: ortak kullanilan bir bilgisayarda oturum kapandiktan
        // sonra belge onbellekten geri gelebilirdi.
        Response.Headers.CacheControl = sonuc.Value.IsPublic
            ? "public, max-age=31536000, immutable"
            : "private, no-store";

        return File(sonuc.Value.Content, sonuc.Value.ContentType);
    }

    /// <summary>Gorsel yukler ve dosya kaydini olusturur.</summary>
    /// <remarks>
    /// Yalnizca .jpg, .jpeg, .png ve .webp kabul edilir; uzantiya degil
    /// dosyanin ilk baytlarina bakilarak dogrulanir. En fazla 5 MB.
    ///
    /// <para>
    /// <b>Gun 5'te AdminOnly'den OrganizerOnly'ye cekildi.</b> Gun 4'te tek
    /// kullanim mekan kapak gorseliydi ve mekan yonetimi admin isi. Ancak
    /// sartname Sprint 5'te organizatore "gorsel yukleme" gorevini veriyor
    /// ve etkinlik afisi yayina alma on kosulu: admin'e bagli kalsaydi
    /// organizator kendi etkinligini yayina hazir hâle getiremezdi.
    /// </para>
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.OrganizerOnly)]
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

    /// <summary>Belge yukler (sahne kira sozlesmesi, izin yazisi).</summary>
    /// <remarks>
    /// Gorsel ucundan AYRI, cunku kabul ettigi turler farkli: burada PDF de
    /// gecerli. Tek uc olup turu istek govdesinden alsaydi istemci afis
    /// alanina PDF yukleyebilir ve vitrinde cizilemeyen bir "gorsel"
    /// olusurdu.
    ///
    /// <para>
    /// Tur yine uzantiya degil <b>icerigin ilk baytlarina</b> bakilarak
    /// dogrulaniyor: <c>%PDF</c> imzasi tasimayan bir dosya <c>.pdf</c>
    /// adiyla gonderilse de reddediliyor.
    /// </para>
    /// </remarks>
    [HttpPost("belge")]
    [Authorize(Policy = Policies.OrganizerOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<UploadedFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadDocument(
        IFormFile dosya, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dosya);

        await using var icerik = dosya.OpenReadStream();

        var command = new UploadFileCommand(
            icerik, dosya.FileName, dosya.ContentType, dosya.Length, Belge: true);

        return ToResponse(await sender.Send(command, cancellationToken));
    }
}
