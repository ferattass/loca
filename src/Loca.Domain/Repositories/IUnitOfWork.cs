namespace Loca.Domain.Repositories;

/// <summary>
/// Degisiklikleri tek noktadan kaydeder.
/// </summary>
/// <remarks>
/// Handler'lar <c>DbContext</c>'i degil bu arayuzu gorur. Gun 6'da rezervasyon
/// akisinda koltuk durumu, rezervasyon ve kalemler TEK transaction icinde
/// yazilacak; o transaction siniri burada yonetilir.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
