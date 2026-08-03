using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

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

    void Add(OutboxMessage message);
}
