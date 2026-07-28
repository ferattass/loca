using Loca.Application.Common.Interfaces;

namespace Loca.Infrastructure.Services;

internal sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// BCrypt maliyet katsayisi. Her artis ozetleme suresini iki katina cikarir.
    /// </summary>
    /// <remarks>
    /// 12, gunumuz donanimlarinda ozet basina yaklasik 200-300 ms demek:
    /// kullanicinin fark etmeyecegi, saldirganin kaba kuvvet denemesini ise
    /// pratikte imkansiz kilacak bir sure. Donanim hizlandikca artirilir.
    /// </remarks>
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Veritabanindaki ozet bozuksa veya baska bir algoritmayla
            // uretilmisse istisna firlatmak yerine "dogrulanamadi" denir.
            // Aksi hâlde tek bozuk kayit giris endpoint'ini 500'e dusururdu.
            return false;
        }
    }
}
