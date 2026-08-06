using System.Globalization;
using System.Net;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Loca.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loca.Infrastructure.Email;

/// <summary>
/// Sifirlama baglantisini e-postayla gonderir.
/// </summary>
/// <remarks>
/// SMTP yapilandirilmamissa gelistirme uygulamasina (log'a yazan)
/// DUSMUYOR: sessizce log'a dusseydi, uretimde SMTP'yi kurmayi unutan
/// biri hicbir uyari almadan token'lari log dosyasina yazdirmis olurdu.
/// Hata yukari birakiliyor ve kullanici "su an gonderemedik" cevabini
/// aliyor.
///
/// <para>
/// <b>Token log'a yazilmiyor.</b> Log'u goren herkes istedigi hesabin
/// sifresini degistirebilirdi.
/// </para>
/// </remarks>
internal sealed class EmailPasswordResetNotifier(
    IEmailSender sender,
    IUserRepository users,
    IConfiguration configuration,
    ILogger<EmailPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public async Task SendAsync(
        string email,
        string token,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var kok = (configuration["WebApp:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');

        // Token sorgu dizesinde tasiniyor cunku kullanici bir baglantiya
        // tiklayacak. Bu, token'in tarayici gecmisine dusmesi demek; bu
        // yuzden token kisa omurlu ve tek kullanimlik.
        var baglanti = $"{kok}/sifre-sifirla?token={WebUtility.UrlEncode(token)}";

        var kullanici = await users.GetByEmailAsync(email, cancellationToken);
        var adSoyad = kullanici?.FullName ?? string.Empty;

        var sonGecerlilik = expiresAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        // Ad HTML'e KACISLI yaziliyor. Kullanici adini kendisi giriyor ve
        // icinde etiket olan bir ad, postayi acan kisinin istemcisinde
        // calisan bir icerik hâline gelirdi.
        var selamlama = adSoyad.Length > 0
            ? $"Merhaba {WebUtility.HtmlEncode(adSoyad)},"
            : "Merhaba,";

        var govde = $"""
            <p>{selamlama}</p>
            <p>Loca hesabının şifresini sıfırlamak için aşağıdaki bağlantıya tıkla:</p>
            <p><a href="{baglanti}">Şifremi sıfırla</a></p>
            <p>Bu bağlantı {sonGecerlilik} (UTC) tarihine kadar geçerli ve bir kez kullanılabilir.</p>
            <p>Bu isteği sen yapmadıysan bu postayı yok sayabilirsin; şifren değişmez.</p>
            """;

        await sender.SendAsync(
            new EmailMessage(email, adSoyad, "Loca — şifre sıfırlama", govde), cancellationToken);

        // Token YOK, yalnizca maskelenmis adres: gonderimin gerceklestigi
        // izlenebilsin ama log'u goren biri hesabi ele geciremesin.
        logger.LogInformation(
            "Sifre sifirlama postasi gonderildi. Alici: {Eposta}", Masking.Email(email));
    }
}
