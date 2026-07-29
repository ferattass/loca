using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Loca.Infrastructure.Services;

/// <summary>
/// Sifirlama token'ini gelistirme log'una yazar.
/// </summary>
/// <remarks>
/// Yol haritasi Gun 3: "gercek e-posta gonderme yok; simdilik token'i
/// Development log'una bas." Gun 9'da Mailpit uzerinden calisan SMTP
/// uygulamasi bunun yerini alacak.
///
/// <para>
/// Uyari seviyesinde yazilmasi bilincli: bu satirin uretim log'unda
/// gorunmesi bir yapilandirma hatasidir ve goze carpmali.
/// </para>
/// </remarks>
internal sealed class DevelopmentPasswordResetNotifier(
    ILogger<DevelopmentPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public Task SendAsync(
        string email,
        string token,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "GELISTIRME: {Eposta} icin sifre sifirlama token'i uretildi. " +
            "Gecerlilik: {Gecerlilik:u}. Token: {Token}",
            Masking.Email(email),
            expiresAt,
            token);

        return Task.CompletedTask;
    }
}
