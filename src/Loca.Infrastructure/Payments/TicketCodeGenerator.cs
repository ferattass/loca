using System.Security.Cryptography;
using Loca.Application.Common.Interfaces;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Bilet numarasi ve QR kodu ureticisi.
/// </summary>
/// <remarks>
/// Ikisi de kriptografik rastgelelikten uretiliyor. <c>Random</c> yeterli
/// gorunuyor ama degil: tohumu tahmin edilebilir oldugu icin uretilen
/// kodlar da tahmin edilebilir olur ve baskasinin biletini denemeyle bulmak
/// mumkun hâle gelir.
/// </remarks>
internal sealed class TicketCodeGenerator : ITicketCodeGenerator
{
    /// <summary>
    /// Karistirilabilen karakterler cikarildi: I, O, 0, 1.
    /// </summary>
    /// <remarks>
    /// Bilet numarasi telefonda okunuyor ve elle yaziliyor. "0" ile "O",
    /// "1" ile "I" ayrimini kullaniciya biraktirmak destek cagrisi uretir.
    /// </remarks>
    private const string Alfabe = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string NewTicketNumber() => $"LOCA-{Blok(4)}-{Blok(4)}";

    /// <remarks>
    /// QR icerigi elle yazilmiyor, bu yuzden daha uzun ve daha genis
    /// alfabeli: 32 karakterlik URL-guvenli deger.
    /// </remarks>
    public string NewQrCode()
    {
        var bayt = RandomNumberGenerator.GetBytes(24);

        return Convert.ToBase64String(bayt)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Blok(int uzunluk)
    {
        var karakterler = new char[uzunluk];

        for (var i = 0; i < uzunluk; i++)
            karakterler[i] = Alfabe[RandomNumberGenerator.GetInt32(Alfabe.Length)];

        return new string(karakterler);
    }
}
