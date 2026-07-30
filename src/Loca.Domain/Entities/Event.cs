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
        CancellationPolicy = string.Empty;
    }

    public Event(
        Guid organizerId,
        Guid categoryId,
        string title,
        string description,
        EventPlace place,
        EventSchedule schedule,
        string cancellationPolicy,
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

        if (string.IsNullOrWhiteSpace(cancellationPolicy))
            throw new DomainException("Iptal ve iade politikasi bos olamaz.");

        if (minimumAge is < 0 or > 99)
            throw new DomainException("Yas siniri 0 ile 99 arasinda olmali.");

        OrganizerId = organizerId;
        CategoryId = categoryId;
        Title = title.Trim();
        Description = description.Trim();
        CancellationPolicy = cancellationPolicy.Trim();
        MinimumAge = minimumAge;

        ApplyPlace(place);
        ApplySchedule(schedule);
    }

    /// <summary>Etkinligi olusturan organizator. Kaynak sahipligi bu alana bakar.</summary>
    public Guid OrganizerId { get; private set; }

    public Guid CategoryId { get; private set; }
    public EventCategory? Category { get; private set; }

    public string Title { get; private set; }
    public string Description { get; private set; }

    /// <summary>Iptal ve iade kosullarinin kullaniciya gosterilen metni.</summary>
    public string CancellationPolicy { get; private set; }

    public Guid CityId { get; private set; }
    public City? City { get; private set; }

    public Guid VenueId { get; private set; }
    public Venue? Venue { get; private set; }

    public Guid HallId { get; private set; }
    public Hall? Hall { get; private set; }

    /// <summary>
    /// Etkinligi olusturan kullanici.
    /// </summary>
    /// <remarks>
    /// Navigation ozelligi okuma tarafi icin var: etkinlik detayi
    /// projeksiyonda <c>Organizer.FullName</c> diyerek tek sorguda
    /// organizator adini alabiliyor. Yazma tarafinda kullanilmiyor,
    /// sahiplik kontrolu <see cref="OrganizerId"/> uzerinden.
    /// </remarks>
    public User? Organizer { get; private set; }

    /// <summary>
    /// Duyurulan baslangic ani. Ana listeleme index'inin ucuncu kolonu.
    /// </summary>
    /// <remarks>
    /// Oturumlarin da kendi tarihleri var; bu alan onlarin yerine gecmiyor.
    /// Listeleme sorgusu <c>(CityId, CategoryId, EventDateUtc)</c> uzerinden
    /// gittigi icin etkinligin tarihi burada duz kolon olarak da duruyor —
    /// aksi hâlde her listeleme oturum tablosuna join atmak zorunda kalirdi.
    /// Tutarli kalmasi <see cref="AddSession"/> ve yayin on kosulu ile
    /// garanti ediliyor.
    /// </remarks>
    public DateTime EventDateUtc { get; private set; }

    public int DurationMinutes { get; private set; }

    public DateTime SalesStartsAtUtc { get; private set; }
    public DateTime SalesEndsAtUtc { get; private set; }

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

    /// <summary>Duz kolonlarin deger nesnesi hâli.</summary>
    public EventPlace Place => new(CityId, VenueId, HallId);

    /// <inheritdoc cref="Place"/>
    public EventSchedule Schedule =>
        new(EventDateUtc, DurationMinutes, SalesStartsAtUtc, SalesEndsAtUtc);

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

    /// <summary>
    /// Kritik olmayan alanlari gunceller.
    /// </summary>
    /// <remarks>
    /// Baslik, aciklama, kategori, yas siniri ve iptal politikasi yayindan
    /// sonra da degistirilebilir: bunlar bilet alma kararini bozmaz, duzeltme
    /// ihtiyaci ise gercek (yazim hatasi, eksik aciklama). Tarih ve salon
    /// icin ayri metotlar var cunku onlar kritik.
    /// </remarks>
    public void UpdateDetails(
        string title,
        string description,
        Guid categoryId,
        string cancellationPolicy,
        int? minimumAge)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new DomainException("Iptal edilmis veya tamamlanmis etkinlik guncellenemez.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Etkinlik basligi bos olamaz.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Etkinlik aciklamasi bos olamaz.");

        if (categoryId == Guid.Empty)
            throw new DomainException("Etkinlik bir kategoriye bagli olmali.");

        if (string.IsNullOrWhiteSpace(cancellationPolicy))
            throw new DomainException("Iptal ve iade politikasi bos olamaz.");

        if (minimumAge is < 0 or > 99)
            throw new DomainException("Yas siniri 0 ile 99 arasinda olmali.");

        Title = title.Trim();
        Description = description.Trim();
        CategoryId = categoryId;
        CancellationPolicy = cancellationPolicy.Trim();
        MinimumAge = minimumAge;
    }

    /// <summary>
    /// Sehir, mekan veya salonu degistirir.
    /// </summary>
    /// <remarks>
    /// Kritik alan: yayindan sonra kilitli. Bilet almis kullanici afiste
    /// yazan salona gider; salon sessizce degisirse yanlis adrese gider.
    /// </remarks>
    public void ChangePlace(EventPlace place)
    {
        EnsureCriticalChangeAllowed("Yayina alinmis etkinligin yeri degistirilemez.");
        ApplyPlace(place);
    }

    /// <summary>
    /// Tarih ve satis penceresini degistirir.
    /// </summary>
    /// <remarks>
    /// Kritik alan: yayindan sonra kilitli. Ayrica oturumlarla tutarli
    /// kalmasi gerekir; oturumu olan bir etkinlikte tarih, en erken oturumun
    /// baslangicindan once cekilemez.
    /// </remarks>
    public void Reschedule(EventSchedule schedule)
    {
        EnsureCriticalChangeAllowed("Yayina alinmis etkinligin tarihi degistirilemez.");

        // Oturum varken duyurulan tarih serbestce degistirilemez: degisseydi
        // etkinlik tarihi ile ilk oturum ayrisir ve yayin ani anlasilmaz bir
        // hata verirdi. Dogru is akisi oturumu tasimak; etkinlik tarihi ona
        // kendiliginden hizalanir.
        if (EarliestSessionStart() is { } enErken && schedule.EventDateUtc != enErken)
        {
            throw new DomainException(
                "Oturumu olan etkinligin tarihi dogrudan degistirilemez. " +
                "Once oturumun tarihini tasiyin; etkinlik tarihi ona gore guncellenir.");
        }

        ApplySchedule(schedule);
    }

    public void SetPoster(Guid? posterFileId) => PosterFileId = posterFileId;

    private void EnsureCriticalChangeAllowed(string message)
    {
        if (!AllowsCriticalChanges)
            throw new DomainException($"{message} Mevcut durum: {Status}.");
    }

    private void ApplyPlace(EventPlace place)
    {
        CityId = place.CityId;
        VenueId = place.VenueId;
        HallId = place.HallId;
    }

    private void ApplySchedule(EventSchedule schedule)
    {
        EventDateUtc = schedule.EventDateUtc;
        DurationMinutes = schedule.DurationMinutes;
        SalesStartsAtUtc = schedule.SalesStartsAtUtc;
        SalesEndsAtUtc = schedule.SalesEndsAtUtc;
    }

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

        // Iptal edilmis oturumlar sayilmaz: hepsi iptalse etkinlik listede
        // gorunur ama satin alinabilecek hicbir seans yoktur.
        var enErken = EarliestSessionStart()
            ?? throw new DomainException("Yayin icin iptal edilmemis en az bir oturum gerekli.");

        if (_ticketTypes.Count == 0)
            throw new DomainException("Yayin icin en az bir bilet turu gerekli.");

        if (!_ticketTypes.Any(ticketType => ticketType.IsActive))
            throw new DomainException("Yayin icin en az bir aktif bilet turu gerekli.");

        if (PosterFileId is null)
            throw new DomainException("Yayin icin afis gorseli gerekli.");

        // Duyurulan tarih ile ilk oturum ayrisirsa listede yazan tarih
        // yaniltici olur. Oturum eklenirken hizalaniyor; burada son kontrol.
        if (EventDateUtc != enErken)
        {
            throw new DomainException(
                "Duyurulan etkinlik tarihi ile en erken oturumun baslangici ayni olmali.");
        }
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

        // Etkinligin satisi kapandiktan sonra baslamayan bir oturum, "bilet
        // hâlâ satiliyorken perde acilmis" demektir. Ayrica bu kural
        // olmadan asagidaki tarih hizalamasi etkinlik tarihini satis
        // bitisinin oncesine cekip EventSchedule kuralini bozabilirdi.
        if (startsAtUtc < SalesEndsAtUtc)
        {
            throw new DomainException(
                "Oturum, etkinligin bilet satisi kapanmadan once baslayamaz.");
        }

        var yeni = new EventSession(
            Id, hallId, seatLayoutId, startsAtUtc, endsAtUtc, salesStartsAtUtc, salesEndsAtUtc);

        _sessions.Add(yeni);

        AlignEventDateWithSessions();

        return yeni;
    }

    /// <summary>
    /// Duyurulan etkinlik tarihini en erken oturumun baslangicina esitler.
    /// </summary>
    /// <remarks>
    /// Iki ayri tarih tutuldugu anda kacinilmaz soru su olur: hangisi dogru?
    /// Cevap "en erken oturum" — kullanicinin listede gordugu tarih perdenin
    /// gercekten acildigi andir. Elle senkron tutmak yerine oturum eklendikce
    /// otomatik hizalaniyor; yayin on kosulu da bunu ayrica dogruluyor.
    /// </remarks>
    private void AlignEventDateWithSessions()
    {
        var enErken = EarliestSessionStart();

        if (enErken is { } baslangic && baslangic != EventDateUtc)
            ApplySchedule(Schedule.WithEventDate(baslangic));
    }

    private DateTime? EarliestSessionStart() =>
        _sessions
            .Where(session => session.Status != EventSessionStatus.Cancelled)
            .Select(session => (DateTime?)session.StartsAtUtc)
            .Min();

    public TicketType AddTicketType(
        string name,
        Money price,
        int quota,
        DateTime salesStartsAtUtc,
        DateTime salesEndsAtUtc,
        bool requiresVerification = false,
        Guid? seatSectionId = null)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new DomainException($"Bu durumdaki etkinlige bilet turu eklenemez: {Status}.");

        // UNIQUE(EventId, Name) veritabaninda da var; buradaki kontrol
        // kullaniciya anlasilir bir mesaj dondurmek icin.
        if (_ticketTypes.Any(existing =>
                string.Equals(existing.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Bu etkinlikte '{name.Trim()}' adinda bir bilet turu zaten var.");
        }

        var yeni = new TicketType(
            Id, name, price, quota, salesStartsAtUtc, salesEndsAtUtc, requiresVerification);

        _ticketTypes.Add(yeni);

        if (seatSectionId is not null)
            AssignTicketTypeToSection(yeni.Id, seatSectionId);

        return yeni;
    }

    /// <summary>
    /// Bilet turunu koltuk bolumune atar.
    /// </summary>
    /// <remarks>
    /// Sartnamenin kurali: <b>ayni koltuk birden fazla aktif bilet turune
    /// atanamaz.</b> Koltuk bazinda kontrol etmek 600 satir gezmek demek;
    /// atama bolum bazinda yapildigi icin kural "ayni bolum iki aktif ture
    /// atanamaz" hâline geliyor ve aggregate icinde, veritabanina gitmeden
    /// dogrulanabiliyor.
    ///
    /// <para>
    /// Ayni kontrol handler'da yazilsaydi bilet turu ekleme ve bolum atama
    /// uclarinin ikisinde de tekrar edilmesi gerekirdi; biri unutuldugunda
    /// koltuk uretiminde ayni koltuga iki fiyat cikardi.
    /// </para>
    /// </remarks>
    public void AssignTicketTypeToSection(Guid ticketTypeId, Guid? seatSectionId)
    {
        var ticketType = _ticketTypes.FirstOrDefault(candidate => candidate.Id == ticketTypeId)
            ?? throw new DomainException("Bilet turu bu etkinlige ait degil.");

        if (seatSectionId is { } section)
        {
            var cakisan = _ticketTypes.Any(other =>
                other.Id != ticketTypeId &&
                other.IsActive &&
                other.SeatSectionId == section);

            if (cakisan)
            {
                throw new DomainException(
                    "Bu bolum baska bir aktif bilet turune atanmis. " +
                    "Ayni koltuk birden fazla aktif bilet turune atanamaz.");
            }
        }

        ticketType.AssignToSection(seatSectionId);
    }

    /// <summary>
    /// Koltuk uretiminde kullanilacak varsayilan bilet turu.
    /// </summary>
    /// <remarks>
    /// Bolume atanmamis aktif tur varsayilandir. Yoksa <c>null</c> doner ve
    /// uretim, bolumu eslesmeyen koltuk kaldiginda hata verir — fiyatsiz
    /// koltuk uretmekten iyidir.
    /// </remarks>
    public TicketType? DefaultTicketType =>
        _ticketTypes.FirstOrDefault(
            ticketType => ticketType.SeatSectionId is null && ticketType.IsActive);
}
