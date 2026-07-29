using Loca.Application.Common.Files;

namespace Loca.UnitTests.Application;

/// <summary>
/// Yuklenen dosyanin tur dogrulamasi.
/// </summary>
/// <remarks>
/// Bu kontrol olmasa <c>zararli.exe</c> dosyasi <c>resim.png</c> adiyla ve
/// dogru MIME basligiyla sunucuya yazilabilirdi; uzantiyi da MIME basligini
/// da gonderen taraf belirliyor.
/// </remarks>
public class ImageSignatureTests
{
    private static byte[] Basliga(params byte[] imza)
    {
        var tampon = new byte[ImageSignature.RequiredHeaderLength];
        imza.CopyTo(tampon, 0);
        return tampon;
    }

    [Fact]
    public void PngHeaderShouldBeDetected()
    {
        var icerik = Basliga(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);

        Assert.Equal(".png", ImageSignature.Tespit(icerik));
    }

    [Fact]
    public void JpegHeaderShouldBeDetected()
    {
        var icerik = Basliga(0xFF, 0xD8, 0xFF, 0xE0);

        Assert.Equal(".jpg", ImageSignature.Tespit(icerik));
    }

    /// <remarks>
    /// WEBP iki parcali: dosya RIFF ile baslar, aradaki dort bayt uzunluk
    /// bilgisidir ve degisir, sekizinci bayttan itibaren WEBP gelir.
    /// </remarks>
    [Fact]
    public void WebpHeaderShouldBeDetected()
    {
        var icerik = new byte[]
        {
            0x52, 0x49, 0x46, 0x46,   // RIFF
            0x24, 0x00, 0x00, 0x00,   // uzunluk (degisken)
            0x57, 0x45, 0x42, 0x50    // WEBP
        };

        Assert.Equal(".webp", ImageSignature.Tespit(icerik));
    }

    [Fact]
    public void RiffWithoutWebpShouldNotBeDetected()
    {
        // RIFF ile baslayan ama WAV olan bir dosya gorsel sayilmamali.
        var icerik = new byte[]
        {
            0x52, 0x49, 0x46, 0x46,
            0x24, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45    // WAVE
        };

        Assert.Null(ImageSignature.Tespit(icerik));
    }

    /// <remarks>
    /// Windows calistirilabilir dosyasi "MZ" ile baslar. Adi resim.png
    /// olsa bile icerigi ele veriyor.
    /// </remarks>
    [Fact]
    public void ExecutableDisguisedAsImageShouldBeRejected()
    {
        var icerik = Basliga(0x4D, 0x5A, 0x90, 0x00);

        Assert.Null(ImageSignature.Tespit(icerik));
    }

    [Fact]
    public void EmptyContentShouldNotBeDetected()
    {
        Assert.Null(ImageSignature.Tespit([]));
    }

    [Fact]
    public void TruncatedHeaderShouldNotBeDetected()
    {
        // PNG imzasinin yalnizca ilk iki bayti.
        Assert.Null(ImageSignature.Tespit([0x89, 0x50]));
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".JPG")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    public void AllowedExtensionsShouldPass(string uzanti)
    {
        Assert.True(ImageSignature.UzantiIzinli(uzanti));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".svg")]
    [InlineData(".gif")]
    [InlineData("")]
    [InlineData(null)]
    public void DisallowedExtensionsShouldFail(string? uzanti)
    {
        Assert.False(ImageSignature.UzantiIzinli(uzanti));
    }

    /// <remarks>
    /// .jpeg ile .jpg ayni tur; tespit her zaman .jpg dondugu icin
    /// eslesme kontrolunde ikisi de kabul edilmeli.
    /// </remarks>
    [Theory]
    [InlineData(".jpg", ".jpg")]
    [InlineData(".jpg", ".jpeg")]
    [InlineData(".jpg", ".JPEG")]
    [InlineData(".png", ".png")]
    [InlineData(".webp", ".webp")]
    public void MatchingExtensionsShouldBeAccepted(string tespit, string uzanti)
    {
        Assert.True(ImageSignature.Eslesiyor(tespit, uzanti));
    }

    [Fact]
    public void PngContentWithJpgExtensionShouldNotMatch()
    {
        // Adi degistirilmis dosya: icerik PNG ama uzanti .jpg.
        Assert.False(ImageSignature.Eslesiyor(".png", ".jpg"));
    }
}
