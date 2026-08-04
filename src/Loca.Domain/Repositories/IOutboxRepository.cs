using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <param name="Pending">Hic denenmemis, islenmeyi bekleyen.</param>
/// <param name="Retryable">En az bir kez basarisiz olmus, hakki bitmemis.</param>
/// <param name="DeadLettered">Hakki tukenmis; is akisi bir daha ele almiyor.</param>
public sealed record OutboxQueueCounts(int Pending, int Retryable, int DeadLettered);

public interface IOutboxRepository
{
    /// <summary>
    /// Islenmeyi bekleyen mesajlar; en eski once.
    /// </summary>
    /// <remarks>
    /// Deneme hakki tukenmis (olu mektup) kayitlar DONMEZ. Donseydi is her
    /// turda ayni basarisiz mesaji yeniden dener ve kuyrugun geri kalani
    /// hic islenmezdi.
    /// </remarks>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Daha once en az bir kez basarisiz olmus, hakki bitmemis mesajlar.
    /// </summary>
    /// <remarks>
    /// Ilk deneme ile yeniden deneme AYRI islerde calisiyor ve ayri
    /// sikliklarda tetikleniyor. Ayni iste toplansaydi, surekli basarisiz
    /// olan bir mesaj her turda yeniden denenip yeni mesajlarin sirasini
    /// isgal ederdi; ayrildiginda yeni mesajlar hizli, sorunlular seyrek
    /// isleniyor.
    /// </remarks>
    Task<IReadOnlyList<OutboxMessage>> GetRetryableAsync(
        int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Deneme hakki tukenmis, hâlâ islenmemis mesajlar. Raporlama icin.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Kuyrugun ozeti: bekleyen, yeniden denenecek ve olu mektup sayilari.</summary>
    /// <remarks>
    /// Sayim ayri bir metot; listeleri cekip saymak, panelin acildigi her
    /// seferde kuyrugun tamamini bellege almak olurdu.
    /// </remarks>
    Task<OutboxQueueCounts> CountByStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Tek bir mesaji yeniden denenebilir hâle getirir.</summary>
    /// <remarks>
    /// Deneme hakki tukenmis mesaji is akisi bir daha ele almiyor; sebep
    /// giderildikten sonra (orn. e-posta sunucusu duzeldi) kuyruga geri
    /// koymanin elle bir yolu olmali. Aksi halde tek care veritabanina
    /// dogrudan UPDATE atmak olurdu.
    /// </remarks>
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(OutboxMessage message);
}
