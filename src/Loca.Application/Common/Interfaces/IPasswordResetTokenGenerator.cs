namespace Loca.Application.Common.Interfaces;

/// <param name="Value">Kullaniciya giden duz token. Veritabanina yazilmaz.</param>
/// <param name="Hash">Veritabaninda saklanan ozet.</param>
public sealed record PasswordResetTokenValue(string Value, string Hash, DateTime ExpiresAt);

public interface IPasswordResetTokenGenerator
{
    PasswordResetTokenValue Create();

    /// <summary>
    /// Gelen duz token'in ozetini alir; kayitli ozetle karsilastirmak icin.
    /// </summary>
    string Hash(string token);
}
