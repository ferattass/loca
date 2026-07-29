using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

public interface IPasswordResetTokenRepository
{
    /// <param name="tokenHash">Duz token degil, ozeti.</param>
    Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Bir kullanicinin henuz kullanilmamis ve suresi dolmamis token'lari.</summary>
    /// <remarks>
    /// Yeni sifirlama istegi geldiginde oncekiler gecersiz kilinir. Aksi hâlde
    /// arka arkaya uc istek atan kullanicinin e-posta kutusunda ayni anda
    /// calisan uc baglanti bulunurdu.
    /// </remarks>
    Task<IReadOnlyList<PasswordResetToken>> GetUsableByUserIdAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    void Add(PasswordResetToken token);
}
