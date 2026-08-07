using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Etkinlik belgeleri.
/// </summary>
/// <remarks>
/// <c>Event</c> aggregate'inin icine konmadi: belgeler onay disindaki
/// hicbir islemde okunmuyor ve koleksiyon olarak eklenseydi her etkinlik
/// yuklemesinde bosa tasinirdi. Aggregate siniri "hangi kurallar birlikte
/// dogrulanmali" sorusuna gore cizilir; belge sayisi etkinligin durum
/// gecislerinin hicbirini degistirmiyor — yalnizca onaya gonderme
/// esiginde bir kez soruluyor ve o soru tek bir <c>bool</c> olarak
/// aggregate'e disaridan veriliyor.
/// </remarks>
public interface IEventDocumentRepository
{
    void Add(EventDocument document);

    void Remove(EventDocument document);

    Task<EventDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
