using Loca.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Loca.WebApi.Controllers;

/// <summary>
/// Tum controller'lar icin ortak taban: <see cref="Result"/> nesnesini
/// HTTP cevabina cevirir.
/// </summary>
/// <remarks>
/// Bu cevirinin tek yerde olmasi onemli. Her controller kendi
/// <c>if (result.IsFailure) return BadRequest(...)</c> satirini yazsaydi
/// ayni hata bir endpoint'te 400, digerinde 409 donerdi ve istemci tarafinda
/// guvenilir bir hata isleme yazilamazdi.
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToResponse<TValue>(Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    protected IActionResult ToResponse(Result result, int successStatusCode = StatusCodes.Status204NoContent)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? StatusCode(successStatusCode)
            : ToProblem(result.Error);
    }

    /// <remarks>
    /// Esleme, <c>GlobalExceptionHandler</c> icindeki exception eslemesiyle
    /// ayni mantigi izler; ikisi birlikte "hangi hata hangi durum kodu"
    /// sorusunun tek cevabini olusturur.
    /// </remarks>
    private ObjectResult ToProblem(Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Dogrulama hatasi"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Kimlik dogrulanamadi"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Yetkisiz islem"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Kayit bulunamadi"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Is kurali ihlali"),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatasi")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = error.Message,
            Instance = HttpContext.Request.Path
        };

        // Istemci metne degil koda bakarak karar versin: mesaj metni
        // degistiginde arayuzdeki mantik kirilmasin.
        problemDetails.Extensions["code"] = error.Code;
        problemDetails.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
