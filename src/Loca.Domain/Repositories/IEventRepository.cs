using Loca.Domain.Entities;

namespace Loca.Domain.Repositories;

/// <summary>
/// Bir oturma planindaki aktif koltugun kimligi ve bagli oldugu bolum.
/// </summary>
/// <remarks>
/// Koltuk uretiminde 600 <c>Seat</c> nesnesinin tamami yuklenmiyor: gereken
/// yalnizca kimlik ve bolum. Tam entity cekilseydi konum, etiket ve audit
/// alanlari da bosa tasinirdi.
/// </remarks>
public sealed record SeatPlacement(Guid SeatId, Guid SeatSectionId);

public interface IEventRepository
{
    /// <summary>
    /// Etkinligi oturumlari ve bilet turleriyle birlikte getirir.
    /// </summary>
    /// <remarks>
    /// Durum gecisleri ve yayin on kosullari bu iki koleksiyona bakiyor;
    /// eksik yuklenen bir aggregate "oturum yok" diyerek yanlis hata verirdi.
    /// </remarks>
    Task<Event?> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Koleksiyonlar olmadan, yalnizca etkinligin kendisi.</summary>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sehir - mekan - salon zinciri gercekten birbirine bagli mi.
    /// </summary>
    /// <remarks>
    /// Uc kimlik tek tek var olabilir ama birbirine ait olmayabilir:
    /// Ankara'daki bir mekanin salonu, sehir olarak Istanbul secilerek
    /// kaydedilirse etkinlik sehir filtresinde yanlis yerde cikar.
    /// </remarks>
    Task<bool> PlaceIsValidAsync(
        Guid cityId, Guid venueId, Guid hallId, CancellationToken cancellationToken = default);

    Task<bool> SeatLayoutBelongsToHallAsync(
        Guid seatLayoutId, Guid hallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu salon verilen aralikta baska bir etkinligin oturumuna atanmis mi.
    /// </summary>
    /// <remarks>
    /// Ayni etkinligin oturumlari arasindaki cakisma <c>Event.AddSession</c>
    /// icinde, veritabanina gitmeden kontrol ediliyor. FARKLI etkinliklerin
    /// ayni salondaki cakismasi ise aggregate'in gormedigi satirlari
    /// gerektiriyor; sartnamenin "aynı salon aynı zaman aralığında iki
    /// etkinliğe atanamaz" kuralinin kalan yarisi burada.
    /// </remarks>
    /// <param name="haricEtkinlikId">
    /// Kendi oturumlari sayilmasin diye disarida birakilan etkinlik.
    /// </param>
    Task<bool> HallHasSessionConflictAsync(
        Guid hallId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? haricEtkinlikId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Salon kapasitesi. Bilet turu kontenjani bunu asamaz.</summary>
    Task<int> HallCapacityAsync(Guid hallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Etkinligin bilet turlerinin toplam kontenjani.
    /// </summary>
    /// <param name="haricTicketTypeId">
    /// Guncellemede turun kendi eski kontenjani sayilmasin diye disarida
    /// birakilir; birakilmasaydi ayni kontenjan iki kez toplanirdi.
    /// </param>
    Task<int> TotalQuotaAsync(
        Guid eventId, Guid? haricTicketTypeId = null, CancellationToken cancellationToken = default);

    /// <summary>Etkinlikte satilmis veya rezerve koltuk var mi.</summary>
    Task<bool> HasCommittedSeatsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Bu bilet turunde satilmis veya rezerve koltuk var mi.</summary>
    Task<bool> TicketTypeHasCommittedSeatsAsync(
        Guid ticketTypeId, CancellationToken cancellationToken = default);

    /// <summary>Bir oturma planindaki aktif koltuklarin kimlik ve bolum bilgisi.</summary>
    Task<IReadOnlyList<SeatPlacement>> GetActiveSeatPlacementsAsync(
        Guid seatLayoutId, CancellationToken cancellationToken = default);

    /// <summary>Bu oturum icin koltuk uretilmis mi. Tekrar uretimi engeller.</summary>
    Task<bool> SeatsGeneratedForSessionAsync(
        Guid eventSessionId, CancellationToken cancellationToken = default);

    /// <summary>Toplu koltuk ekleme; tek cagri, tek <c>SaveChanges</c>.</summary>
    void AddEventSeats(IReadOnlyList<EventSeat> eventSeats);

    Task<EventSession?> GetSessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    Task<TicketType?> GetTicketTypeAsync(
        Guid ticketTypeId, CancellationToken cancellationToken = default);

    /// <summary>Bolum, etkinligin salonundaki oturma planina mi ait.</summary>
    Task<bool> SectionBelongsToHallAsync(
        Guid seatSectionId, Guid hallId, CancellationToken cancellationToken = default);

    void Add(Event ev);

    /// <summary>
    /// Aggregate uzerinden olusturulmus oturumu degisiklik takipcisine bildirir.
    /// </summary>
    /// <remarks>
    /// <b>Bu cagri atlanirsa kayit INSERT degil UPDATE olarak gider.</b>
    /// EF, izlenen bir nesnenin koleksiyonunda yeni bir cocuk buldugunda
    /// "yeni mi, yoksa daha once kaydedilmis ama takipten dusmus mu" sorusunu
    /// birincil anahtara bakarak cevapliyor: anahtar doluysa mevcut kayit
    /// sayiyor ve <c>Modified</c> isaretliyor.
    ///
    /// <para>
    /// <see cref="Common.BaseEntity.Id"/> nesne kurulurken
    /// <c>Guid.CreateVersion7()</c> ile doldugu icin anahtar HER ZAMAN dolu.
    /// Dolayisiyla EF hicbir zaman <c>Added</c> demiyor; uretilen
    /// <c>UPDATE ... WHERE Id = ...</c> sifir satir etkiliyor ve
    /// <c>DbUpdateConcurrencyException</c> firliyor — yani hata "koltuk
    /// cakismasi" gibi gorunen bir 409 olarak disariya cikiyor.
    /// </para>
    ///
    /// <para>
    /// Ayni tuzaga Gun 3'te dusulmemisti cunku refresh token'lar da
    /// repository uzerinden aciktan ekleniyor.
    /// </para>
    /// </remarks>
    void AddSession(EventSession session);

    /// <inheritdoc cref="AddSession"/>
    void AddTicketType(TicketType ticketType);

    void AddAuditLog(AuditLog auditLog);

    /// <remarks>Fiziksel silme degil; interceptor isaretlemeye cevirir.</remarks>
    void Remove(Event ev);

    void RemoveTicketType(TicketType ticketType);
}
