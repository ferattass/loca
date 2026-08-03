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

    /// <summary>
    /// Acik bir veritabani transaction'i baslatir.
    /// </summary>
    /// <remarks>
    /// Tek bir <c>SaveChanges</c> kendi ortuk transaction'ini zaten acar;
    /// buna ragmen acik transaction gerekiyor cunku rezervasyon akisinda
    /// koltuklarin durumu OKUNUP dogrulandiktan sonra yaziliyor. Okuma ile
    /// yazma arasindaki bosluk ayni transaction icinde kalmazsa, arada
    /// baskasinin aldigi koltuk uzerine yazilir.
    /// </remarks>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Acik transaction sinirinin omru.
/// </summary>
/// <remarks>
/// <c>Rollback</c> metodu bilerek YOK: <see cref="IAsyncDisposable.DisposeAsync"/>
/// commit edilmemis transaction'i zaten geri alir. Ayri bir rollback metodu
/// olsaydi bir hata yolunda cagrilmasi unutulabilir ve transaction acik
/// kalirdi; <c>await using</c> ile unutmak mumkun degil.
/// </remarks>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
