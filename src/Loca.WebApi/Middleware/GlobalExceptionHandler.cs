using FluentValidation;
using Loca.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loca.WebApi.Middleware;

/// <summary>
/// Yakalanmamis her hatayi RFC 7807 Problem Details cevabina cevirir.
/// </summary>
/// <remarks>
/// Esleme tek yerde durur. Her controller kendi try/catch'ini yazarsa ayni
/// hata bir yerde 400, baska yerde 500 doner ve istemci tarafinda guvenilir
/// bir hata isleme yazilamaz.
///
/// <para>
/// <c>IExceptionHandler</c>, <c>UseExceptionHandler()</c> hattina takilir;
/// ayri bir middleware yazmaya gerek kalmaz ve <c>AddProblemDetails</c> ile
/// tanimlanan ortak alanlar (traceId, instance) kendiliginden eklenir.
/// </para>
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = Translate(exception);

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            // Beklenmeyen hata: tam ayrinti log'a gider, istemci genel mesaj gorur.
            logger.LogError(exception, "Beklenmeyen hata — {Yol}", httpContext.Request.Path);
        }
        else
        {
            // Beklenen durum (dogrulama, is kurali). Stack trace gurultu olur.
            logger.LogWarning(
                "Istek reddedildi — {Tur}: {Mesaj}", exception.GetType().Name, exception.Message);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails Translate(Exception exception) => exception switch
    {
        // Girdinin sekli hatali. Kullanici duzeltebilir, alan bazli liste doner.
        ValidationException validation => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dogrulama hatasi",
            Detail = "Gonderilen alanlardan bazilari gecerli degil.",
            Extensions =
            {
                ["errors"] = validation.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray())
            }
        },

        // Is kurali ihlali: istek bicimsel olarak dogru ama sistemin
        // o anki durumunda yapilamaz. Ornegin yayinlanmis etkinligin silinmesi.
        DomainException domain => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Is kurali ihlali",
            Detail = domain.Message
        },

        // Ayni kaydi iki istek ayni anda guncelledi. Koltuk rezervasyonunda
        // beklenen bir durumdur; istemci yeniden denemeli.
        //
        // Iki tip de esleniyor: Persistence katmani EF'in istisnasini
        // ConcurrencyConflictException'a ceviriyor, ama ceviriden gecmeyen
        // bir yol kalirsa (baska bir DbContext, ham SaveChanges cagrisi)
        // istemci 500 degil yine 409 gormeli.
        ConcurrencyConflictException or DbUpdateConcurrencyException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Kayit az once degisti",
            Detail = "Islem sirasinda kayit baskasi tarafindan guncellendi. Lutfen tekrar deneyin."
        },

        // Benzersizlik ihlali handler'da yakalanmadiysa buraya duser.
        // Kisit adi ISTEMCIYE GITMEZ: index adi sema hakkinda bilgi sizdirir.
        UniqueConstraintViolationException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Kayit zaten mevcut",
            Detail = "Ayni kayit daha once olusturulmus."
        },

        // Geri kalan her sey. Ic mesaj ve stack trace ISTEMCIYE GITMEZ:
        // dosya yolu, tablo adi veya baglanti dizesi sizdirabilir.
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Sunucu hatasi",
            Detail = "Istek islenirken beklenmeyen bir hata olustu."
        }
    };
}
