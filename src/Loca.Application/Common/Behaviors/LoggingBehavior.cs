using System.Diagnostics;
using Loca.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Common.Behaviors;

/// <summary>
/// Her istegin basladigini, ne kadar surdugunu ve nasil sonuclandigini yazar.
/// </summary>
/// <remarks>
/// Yavaslama sikayeti geldiginde "hangi istek yavas" sorusunun cevabi olcum
/// olmadan tahmine kalir. Sure burada tek yerde olculur; handler'lara
/// kronometre kodu dagilmaz.
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Bu esigi asan istekler ayrica uyari olarak isaretlenir.</summary>
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("{Istek} basladi", requestName);

        // Stopwatch nesnesi yerine zaman damgasi: istek basina bir tahsis daha az.
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await next();
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            // Basarisiz Result bir hata degil, beklenen bir sonuctur; bu yuzden
            // exception olarak degil, hata koduyla birlikte bilgi olarak yazilir.
            if (response is Result { IsFailure: true } failed)
            {
                logger.LogWarning(
                    "{Istek} kural geregi reddedildi ({Sure} ms) — {HataKodu}: {HataMesaji}",
                    requestName,
                    elapsed.TotalMilliseconds,
                    failed.Error.Code,
                    failed.Error.Message);

                return response;
            }

            if (elapsed.TotalMilliseconds > SlowRequestThresholdMs)
            {
                logger.LogWarning(
                    "{Istek} yavas tamamlandi ({Sure} ms)", requestName, elapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "{Istek} tamamlandi ({Sure} ms)", requestName, elapsed.TotalMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            // Buradaki amac hatayi yutmak degil, hangi istekte olustugunu
            // kaydetmek. Cevaba donusturme isi WebApi katmanindadir.
            logger.LogError(
                ex,
                "{Istek} hata ile sonlandi ({Sure} ms)",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw;
        }
    }
}
