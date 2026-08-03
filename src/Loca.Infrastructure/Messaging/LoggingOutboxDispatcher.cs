using Loca.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loca.Infrastructure.Messaging;

/// <summary>
/// Outbox mesajini log'a yazan gecici uygulama.
/// </summary>
/// <remarks>
/// Gun 9'da Mailpit uzerinden calisan SMTP uygulamasiyla degistirilecek;
/// outbox isini calistiran kod degismeyecek. Ayni yaklasim
/// <c>DevelopmentPasswordResetNotifier</c> icin de kullanildi.
///
/// <para>
/// <b>Govde oldugu gibi loglanmiyor.</b> Outbox govdeleri kullanici kimligi
/// ve tutar tasiyor; log'a tam JSON yazmak bu veriyi log dosyalarina
/// dagitirdi. Yalnizca tur ve govde uzunlugu yaziliyor.
/// </para>
/// </remarks>
internal sealed class LoggingOutboxDispatcher(ILogger<LoggingOutboxDispatcher> logger)
    : IOutboxDispatcher
{
    public Task DispatchAsync(
        string type, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        logger.LogInformation(
            "Outbox mesaji gonderildi. Tur: {Tur}, Govde: {Uzunluk} karakter",
            type,
            payload?.Length ?? 0);

        return Task.CompletedTask;
    }
}
