using System.Net.Sockets;
using System.Text.RegularExpressions;
using Loca.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace Loca.Infrastructure.Email;

/// <summary>
/// SMTP uzerinden posta gonderir.
/// </summary>
/// <remarks>
/// <b>Neden MailKit, <c>System.Net.Mail.SmtpClient</c> degil:</b> yerlesik
/// istemci Microsoft tarafindan yeni gelistirme icin onerilmiyor; modern
/// TLS ve kimlik dogrulama secenekleri eksik ve zaman asimi davranisi
/// guvenilmez.
///
/// <para>
/// <b>Baglanti her gonderimde aciliyor ve kapaniyor.</b> Uzun omurlu tek
/// bir baglanti daha hizli olurdu ama SMTP baglantilari sunucu tarafinda
/// sessizce dusuruluyor ve dusmus bir baglantiyla gonderim, hicbir uyari
/// vermeden kaybolan postalar uretiyor. Outbox zaten yeniden deneme
/// sagladigi icin maliyet kabul edilebilir.
/// </para>
/// </remarks>
internal sealed partial class SmtpEmailSender(
    SmtpOptionsProvider optionsProvider,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var ayarlar = await optionsProvider.GetAsync(cancellationToken);

        if (!ayarlar.YapilandirilmisMi)
        {
            // Istisna: outbox mesaji "islendi" sayilmamali. Sessizce
            // donulseydi yonetici SMTP'yi hic kurmadigini fark etmez ve
            // butun postalar basariyla gonderilmis gorunurdu.
            throw new InvalidOperationException(
                "SMTP yapilandirilmamis. Yonetim panelinden sunucu ve gonderen adresi girin.");
        }

        var posta = new MimeMessage();
        posta.From.Add(new MailboxAddress(ayarlar.FromName, ayarlar.FromAddress));
        posta.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        posta.Subject = message.Subject;

        // Duz metin karsiligi da ekleniyor: HTML gostermeyen istemcilerde
        // yalnizca HTML gonderilen posta bos gorunur.
        posta.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = DuzMetne(message.HtmlBody)
        }.ToMessageBody();

        using var istemci = new SmtpClient();

        await BaglanAsync(istemci, ayarlar, cancellationToken);
        await istemci.SendAsync(posta, cancellationToken);
        await istemci.DisconnectAsync(quit: true, cancellationToken);

        // Alici adresi loglanmiyor: kisisel veri ve log dosyalari
        // genellikle uygulamadan daha uzun yasiyor.
        logger.LogInformation("E-posta gonderildi. Konu: {Konu}", message.Subject);
    }

    public async Task<string?> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var ayarlar = await optionsProvider.GetAsync(cancellationToken);

        if (!ayarlar.YapilandirilmisMi)
            return "SMTP yapilandirilmamis: sunucu adresi veya gonderen adresi bos.";

        try
        {
            using var istemci = new SmtpClient();

            await BaglanAsync(istemci, ayarlar, cancellationToken);
            await istemci.DisconnectAsync(quit: true, cancellationToken);

            return null;
        }
        catch (Exception hata) when (hata is SmtpCommandException or SmtpProtocolException
            or AuthenticationException or SocketException or IOException or OperationCanceledException)
        {
            // Ham istisna metni disari veriliyor cunku bu ucun tek amaci
            // yoneticinin neyin yanlis oldugunu gormesi; sunucu adi ve
            // hata kodu tam da aradigi bilgi. Sifre bu metinde gecmiyor.
            logger.LogWarning(hata, "SMTP baglanti denemesi basarisiz.");

            return hata.Message;
        }
    }

    private static async Task BaglanAsync(
        SmtpClient istemci, SmtpOptions ayarlar, CancellationToken cancellationToken)
    {
        // UseSsl acikken StartTlsWhenAvailable KULLANILMIYOR: o secenek
        // sunucu STARTTLS duyurmazsa sessizce duz metne dusuyor, yani araya
        // giren biri duyuruyu kirptiginda kullanici adi ve sifre ag uzerinde
        // acik gidiyor ve hicbir hata gorunmuyor. TLS bilerek acildiysa
        // baglanti korunmasiz kurulmak yerine BASARISIZ olmali.
        //
        // 465 ile 587 ayri protokoller: 465'te TLS baglantinin ilk anindan
        // itibaren var (SslOnConnect), 587'de once duz baglanti kurulup
        // STARTTLS ile yukseltiliyor. Ikisi karistirilirsa el sikisma hic
        // baslamiyor. Yerel Mailpit TLS'siz; UseSsl kapaliyken None kaliyor.
        var guvenlik = ayarlar.UseSsl
            ? (ayarlar.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
            : SecureSocketOptions.None;

        await istemci.ConnectAsync(ayarlar.Host, ayarlar.Port, guvenlik, cancellationToken);

        if (!string.IsNullOrWhiteSpace(ayarlar.UserName))
        {
            // MailKit null sifre kabul etmiyor. Kullanici adi girilip sifre
            // bos birakildiginda bos sifreyle deneniyor: karari sunucu
            // veriyor ve donen hata yoneticiye oldugu gibi gosteriliyor.
            // Burada sessizce vazgecilseydi, yanlis yapilandirilmis bir
            // SMTP "kimlik dogrulamasiz" sanilip baska bir hataya donusurdu.
            await istemci.AuthenticateAsync(
                ayarlar.UserName, ayarlar.Password ?? string.Empty, cancellationToken);
        }
    }

    /// <remarks>
    /// Etiketleri soken kaba bir cevrim; amaci okunabilir bir yedek metin
    /// uretmek, birebir donusum degil. Gonderdigimiz postalarin HTML'i
    /// kendi sablonlarimizdan geliyor ve basit.
    /// </remarks>
    private static string DuzMetne(string html) =>
        EtiketDeseni().Replace(html, string.Empty).Trim();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EtiketDeseni();
}
