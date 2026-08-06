namespace Loca.Application.Common.Interfaces;

/// <param name="Subject">Konu satiri.</param>
/// <param name="HtmlBody">
/// HTML govde. Duz metin karsiligi gonderici tarafinda uretiliyor: bazi
/// istemciler HTML gostermiyor ve yalnizca HTML gonderilen posta o
/// istemcilerde bos gorunurdu.
/// </param>
public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody);

/// <summary>
/// E-posta gonderir.
/// </summary>
/// <remarks>
/// <b>Gonderim basarisizsa istisna firlatir.</b> Sessizce yutulsaydi
/// outbox mesaji "islendi" diye isaretlenir ve kullaniciya hicbir zaman
/// ulasmayan bir posta basarili sayilirdi. Istisna, mesajin yeniden
/// denenmesini sagliyor.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sunucuya baglanip kimlik dogrulamayi dener, posta gondermez.</summary>
    /// <remarks>
    /// Yonetim panelindeki "ayarlari sina" dugmesi icin. Gercek posta
    /// gonderip dogrulamak, her denemede birine posta atmak demek olurdu.
    /// </remarks>
    Task<string?> TestConnectionAsync(CancellationToken cancellationToken = default);
}
