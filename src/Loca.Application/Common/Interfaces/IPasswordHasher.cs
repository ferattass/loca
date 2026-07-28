namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Sifre ozetleme. Uygulamasi BCrypt kullanir.
/// </summary>
/// <remarks>
/// Neden BCrypt de MD5/SHA256 degil? MD5 ve SHA aileleri **hizli** olmak icin
/// tasarlandi; sifre ozetlemede hiz saldirganin lehinedir — saniyede milyarlarca
/// deneme yapilabilir. BCrypt kasitli olarak yavastir ve maliyeti (work factor)
/// donanim hizlandikca artirilabilir. Ayrica salt'i ozetin icinde tasir, yani
/// ayni sifreye sahip iki kullanicinin ozeti farkli cikar.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <returns>Ozet bozuksa istisna firlatmaz, <c>false</c> doner.</returns>
    bool Verify(string password, string passwordHash);
}
