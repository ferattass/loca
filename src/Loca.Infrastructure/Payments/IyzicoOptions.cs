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
    [Required]
    [Url]
    public string CallbackUrl { get; set; } = string.Empty;

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
