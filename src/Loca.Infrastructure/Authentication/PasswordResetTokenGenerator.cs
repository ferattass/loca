using System.Security.Cryptography;
using System.Text;
using Loca.Application.Common.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Loca.Infrastructure.Authentication;

internal sealed class PasswordResetTokenGenerator(IDateTimeProvider dateTimeProvider)
    : IPasswordResetTokenGenerator
{
    /// <summary>32 bayt = 256 bit entropi. Refresh token ile ayni olcu.</summary>
    private const int TokenBytes = 32;

    /// <summary>
    /// Yol haritasi Gun 3: sifirlama token'i bir saat yasar.
    /// </summary>
    /// <remarks>
    /// Kisa tutulmasinin sebebi token'in e-posta kutusunda kalmasi. Posta
    /// hesabina sonradan erisen biri aylar oncesinin baglantisiyla sifre
    /// degistirebilmemeli.
    /// </remarks>
    private const int LifetimeHours = 1;

    public PasswordResetTokenValue Create()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(TokenBytes));

        return new PasswordResetTokenValue(
            value,
            Hash(value),
            dateTimeProvider.UtcNow.AddHours(LifetimeHours));
    }

    /// <remarks>
    /// BCrypt degil SHA-256 kullaniliyor. BCrypt sifreler icin kasitli olarak
    /// yavastir; ozetlenen deger burada kullanicinin sectigi bir sifre degil,
    /// 256 bit rastgele bir token. Kaba kuvvetle tahmin edilemeyecegi icin
    /// yavaslatmanin faydasi yok, her dogrulamada gereksiz gecikme olurdu.
    /// </remarks>
    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
