using System.ComponentModel.DataAnnotations;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Iyzico saglayici ayarlari.
/// </summary>
/// <remarks>
/// Sandbox ve canli ortam arasinda gecis tek bir ayarla (<see cref="UseSandbox"/>)
/// yapilir. Taban adres kod icinde sabitlenmez; boylece ortam degisikliginde
/// deploy edilen kod degil yalnizca yapilandirma (appsettings / user-secrets /
/// ortam degiskeni) degisir.
/// </remarks>
public sealed class IyzicoOptions
{
    public const string SectionName = "Iyzico";

    /// <summary>
    /// Iyzico panelinden alinan API anahtari. Sir degildir, istek govdesinde
    /// acikca tasinir; yine de user-secrets'ta tutulur zira SecretKey ile
    /// birlikte tek bir merchant kimlik bilgisi seti olusturur.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Iyzico panelinden alinan gizli anahtar. Yalnizca imza hesabinda
    /// kullanilir; hicbir zaman istek govdesine, loga veya hata mesajina
    /// yazilmaz (bkz. <see cref="IyzicoSignature"/> ve <see cref="IyzicoPaymentProvider"/>).
    /// </summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Checkout form tamamlaninca Iyzico'nun POST ile donecegi adres.
    /// </summary>
    /// <remarks>
    /// Bu adres <b>API'nin</b> ucu (<c>/api/v1/payments/iyzico/callback</c>),
    /// arayuzun degil. Iyzico buraya tarayici uzerinden form POST'u yapiyor;
    /// dogrudan arayuze gonderilseydi tek sayfa uygulama POST govdesindeki
    /// token'i okuyamazdi.
    ///
    /// <para>
    /// Yerel gelistirmede Iyzico <c>localhost</c>'a erisemez; bir tunel
    /// (ngrok vb.) adresi yazilmali. Bkz. <c>docs/05-odeme-iyzico.md</c>.
    /// </para>
    /// </remarks>
    [Required]
    [Url]
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// Odeme bittikten sonra kullanicinin geri gonderilecegi arayuz adresi.
    /// </summary>
    /// <remarks>
    /// Kok adres; uzerine rezervasyon yolu ekleniyor. Kod icinde
    /// sabitlenseydi ayni derleme farkli ortamlarda calisamazdi.
    /// </remarks>
    [Required]
    [Url]
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>
    /// <c>true</c> ise sandbox, <c>false</c> ise canli ortam kullanilir.
    /// </summary>
    /// <remarks>
    /// Varsayilan deger sandbox: yapilandirma unutulursa yanlislikla canli
    /// tahsilat yapmak yerine sandbox'ta kalinir; guvenli varsayilan budur.
    /// </remarks>
    public bool UseSandbox { get; set; } = true;

    /// <summary>
    /// <see cref="UseSandbox"/> degerine gore secilen taban adres.
    /// </summary>
    public string BaseUrl => UseSandbox
        ? "https://sandbox-api.iyzipay.com"
        : "https://api.iyzipay.com";
}
