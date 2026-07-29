using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Etkinlik. Bir veya birden fazla oturumu (seans) olur.
/// </summary>
/// <remarks>
/// <b>Durum gecisleri bu sinifin icinde.</b> Controller veya handler icinde
/// <c>event.Status = EventStatus.Published</c> yazilabilseydi her yeni
/// endpoint kendi kurallarini uydurur ve "hangi durumdan hangisine
/// gecilebilir" sorusunun cevabi koda dagilirdi. Burada tek yerde duruyor;
/// gecersiz gecis <see cref="DomainException"/> firlatir ve API katmani
/// bunu 409'a cevirir.
///
/// <para>
/// Gecerli akis: Draft → PendingApproval → Published → SalesOpen →
/// SalesClosed → Completed.
/// </para>
/// </remarks>
public sealed class Event : BaseEntity, IAggregateRoot, ISoftDeletable, IOwnedResource
{
    private readonly List<EventSession> _sessions = [];
    private readonly List<TicketType> _ticketTypes = [];

    private Event()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public Event(
        Guid organizerId,
        Guid categoryId,
        string title,
        string description,
        int? minimumAge = null)
    {
        if (organizerId == Guid.Empty)
            throw new DomainException("Etkinlik bir organizatore bagli olmali.");

        if (categoryId == Guid.Empty)
            throw new DomainException("Etkinlik bir kategoriye bagli olmali.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Etkinlik basligi bos olamaz.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Etkinlik aciklamasi bos olamaz.");

        if (minimumAge is < 0 or > 99)
            throw new DomainException("Yas siniri 0 ile 99 arasinda olmali.");

        OrganizerId = organizerId;
        CategoryId = categoryId;
        Title = title.Trim();
        Description = description.Trim();
        MinimumAge = minimumAge;
    }

    /// <summary>Etkinligi olusturan organizator. Kaynak sahipligi bu alana bakar.</summary>
    public Guid OrganizerId { get; private set; }

    public Guid CategoryId { get; private set; }
    public EventCategory? Category { get; private set; }

    public string Title { get; private set; }
    public string Description { get; private set; }

    /// <summary>Afis gorseli. Yayina alabilmek icin zorunlu.</summary>
    public Guid? PosterFileId { get; private set; }

    public int? MinimumAge { get; private set; }

    public EventStatus Status { get; private set; } = EventStatus.Draft;

    public DateTime? PublishedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyCollection<EventSession> Sessions => _sessions;
    public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes;

    Guid IOwnedResource.OwnerId => OrganizerId;

    /// <summary>Satis suregelen bir durumda mi.</summary>
    public bool IsSalesActive => Status == EventStatus.SalesOpen;

    /// <summary>
    /// Kritik alanlar (tarih, salon, oturma plani) serbestce degistirilebilir mi.
    /// </summary>
    /// <remarks>
    /// Yayina cikmis bir etkinligin salonu sessizce degistirilirse bilet almis
    /// kullanici baska bir salona gider. Bu yuzden yayin sonrasi kritik alanlar
    /// kilitli.
    /// </remarks>
    public bool AllowsCriticalChanges =>
        Status is EventStatus.Draft or EventStatus.PendingApproval;

    public void UpdateDetails(string title, string description, Guid categoryId, int? minimumAge)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new DomainException("Iptal edilmis veya tamamlanmis etkinlik guncellenemez.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Etkinlik basligi bos olamaz.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Etkinlik aciklamasi bos olamaz.");

        if (categoryId == Guid.Empty)
            throw new DomainException("Etkinlik bir kategoriye bagli olmali.");

        if (minimumAge is < 0 or > 99)
            throw new DomainException("Yas siniri 0 ile 99 arasinda olmali.");

        Title = title.Trim();
        Description = description.Trim();
        CategoryId = categoryId;
        MinimumAge = minimumAge;
    }

    public void SetPoster(Guid? posterFileId) => PosterFileId = posterFileId;

    /// <summary>Onaya gonderir: Draft → PendingApproval.</summary>
    public void SubmitForApproval()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException($"Yalnizca taslak etkinlik onaya gonderilebilir. Mevcut durum: {Status}.");

        EnsureReadyForPublish();

        Status = EventStatus.PendingApproval;
    }

    /// <summary>Admin onayi: PendingApproval → Published.</summary>
    public void Publish(DateTime utcNow)
    {
        if (Status != EventStatus.PendingApproval)
            throw new DomainException($"Yalnizca onay bekleyen etkinlik yayinlanabilir. Mevcut durum: {Status}.");

        // Onaya gonderilirken dogrulanmisti ama arada oturum silinmis
        // olabilir; yayin ani son kontrol noktasi.
        EnsureReadyForPublish();

        Status = EventStatus.Published;
        PublishedAt = utcNow;
    }

    /// <summary>Satisi acar: Published → SalesOpen.</summary>
    public void OpenSales()
    {
        if (Status != EventStatus.Published)
            throw new DomainException($"Satis yalnizca yayinlanmis etkinlikte acilabilir. Mevcut durum: {Status}.");

        Status = EventStatus.SalesOpen;
    }

    /// <summary>Satisi kapatir: SalesOpen → SalesClosed.</summary>
    public void CloseSales()
    {
        if (Status != EventStatus.SalesOpen)
            throw new DomainException($"Satis yalnizca acikken kapatilabilir. Mevcut durum: {Status}.");

        Status = EventStatus.SalesClosed;
    }

    /// <summary>Etkinligi tamamlar: SalesClosed → Completed.</summary>
    public void Complete()
    {
        if (Status is not (EventStatus.SalesClosed or EventStatus.SalesOpen))
            throw new DomainException($"Bu durumdaki etkinlik tamamlanamaz: {Status}.");

        Status = EventStatus.Completed;
    }

    /// <summary>
    /// Iptal eder. Biletler iade surecine girer (Gun 7).
    /// </summary>
    /// <remarks>
    /// Tamamlanmis etkinlik iptal edilemez: gecmis degistirilemez, satilan
    /// biletler kullanilmis durumda.
    /// </remarks>
    public void Cancel(DateTime utcNow, string reason)
    {
        if (Status == EventStatus.Completed)
            throw new DomainException("Tamamlanmis etkinlik iptal edilemez.");

        if (Status == EventStatus.Cancelled)
            throw new DomainException("Etkinlik zaten iptal edilmis.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Iptal gerekcesi zorunludur.");

        Status = EventStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason.Trim();

        foreach (var session in _sessions)
            session.Cancel();
    }

    /// <summary>Admin tarafindan gecici durdurma.</summary>
    public void Suspend()
    {
        if (Status is not (EventStatus.Published or EventStatus.SalesOpen))
            throw new DomainException($"Bu durumdaki etkinlik askiya alinamaz: {Status}.");

        Status = EventStatus.Suspended;
    }

    /// <summary>Askidan geri alir; satis degil yayin durumuna doner.</summary>
    public void Resume()
    {
        if (Status != EventStatus.Suspended)
            throw new DomainException($"Yalnizca askidaki etkinlik geri alinabilir. Mevcut durum: {Status}.");

        Status = EventStatus.Published;
    }

    /// <summary>
    /// Yayin on kosullari: en az bir oturum, en az bir bilet turu ve afis.
    /// </summary>
    /// <remarks>
    /// Bu kontrol olmadan yayinlanan bir etkinlik listede gorunur ama
    /// kullanici bilet alamaz — hata vermeyen, yalnizca ise yaramayan bir kayit.
    /// </remarks>
    private void EnsureReadyForPublish()
    {
        if (_sessions.Count == 0)
            throw new DomainException("Yayin icin en az bir oturum gerekli.");

        if (_ticketTypes.Count == 0)
            throw new DomainException("Yayin icin en az bir bilet turu gerekli.");

        if (PosterFileId is null)
            throw new DomainException("Yayin icin afis gorseli gerekli.");
    }

    /// <summary>
    /// Oturum ekler. Ayni etkinligin oturumlari arasindaki salon cakismasi
    /// burada kontrol edilir.
    /// </summary>
    /// <remarks>
    /// Oturum disarida olusturulup listeye eklenseydi bu kontrol atlanabilirdi.
    /// Farkli etkinliklerin ayni salondaki cakismasi burada gorulemez —
    /// o kontrol veritabani sorgusu gerektirir ve handler'da yapilir.
    /// </remarks>
    public EventSession AddSession(
        Guid hallId,
        Guid seatLayoutId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc)
    {
        if (!AllowsCriticalChanges && Status != EventStatus.Published)
            throw new DomainException($"Bu durumdaki etkinlige oturum eklenemez: {Status}.");

        var cakisan = _sessions.FirstOrDefault(session =>
            session.HallId == hallId &&
            session.Status != EventSessionStatus.Cancelled &&
            session.OverlapsWith(startsAtUtc, endsAtUtc));

        if (cakisan is not null)
        {
            throw new DomainException(
                "Bu salonda ayni saatlerde baska bir oturum var. " +
                "Oturumlar arasinda en az bir saat temizlik payi birakilmali.");
        }

        var yeni = new EventSession(
            Id, hallId, seatLayoutId, startsAtUtc, endsAtUtc, salesStartsAtUtc, salesEndsAtUtc);

        _sessions.Add(yeni);

        return yeni;
    }

    public TicketType AddTicketType(
        string name,
        Money price,
        int quota,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc,
        bool requiresVerification = false)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new DomainException($"Bu durumdaki etkinlige bilet turu eklenemez: {Status}.");

        var yeni = new TicketType(
            Id, name, price, quota, salesStartsAtUtc, salesEndsAtUtc, requiresVerification);

        _ticketTypes.Add(yeni);

        return yeni;
    }
}
