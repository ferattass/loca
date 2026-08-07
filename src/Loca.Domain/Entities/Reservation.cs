using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Rezervasyona alinacak tek bir koltugun girdisi.
/// </summary>
/// <remarks>
/// Handler <c>EventSeat</c> nesnelerinin tamamini aggregate'e gecirmiyor:
/// rezervasyonun ihtiyaci olan yalnizca kimlik, tur ve fiyat. Entity
/// gecirilseydi aggregate baska bir aggregate'in ic durumunu degistirebilir
/// hâle gelirdi.
/// </remarks>
public sealed record ReservationLine(Guid EventSeatId, Guid TicketTypeId, Money Price);

/// <summary>
/// Bir kullanicinin belirli bir oturumda tuttugu koltuklar.
/// </summary>
/// <remarks>
/// <b>Projenin kalbi.</b> Rezervasyon, koltuklarin gecici olarak kilitlendigi
/// ve odemenin beklendigi araligi temsil eder. Kilit suresi dolarsa koltuklar
/// otomatik geri doner; boyle bir sure olmasaydi bir kullanicinin secip
/// odemedigi koltuk sonsuza kadar satilamaz hâlde kalirdi.
///
/// <para>
/// <b>Toplam tutar HER ZAMAN burada hesaplanir</b>, istekten alinmaz. Istemci
/// tutar gonderseydi araya giren biri 450 TL'lik koltugu 1 TL'ye rezerve
/// edebilirdi. Kalemlerin fiyati da rezervasyon aninda kopyalanir.
/// </para>
///
/// <para>
/// Sureler ve bilet limiti sabit degil, disaridan geliyor: <c>.env</c>'de
/// tanimli (<c>RESERVATION_LOCK_MINUTES</c>, <c>RESERVATION_EXTEND_MINUTES</c>,
/// <c>MAX_SEATS_PER_USER_PER_SESSION</c>). Koda gomulseydi kilit suresinin
/// dolmasini bekleyen bir test on dakika calisirdi.
/// </para>
/// </remarks>
public sealed class Reservation : BaseEntity, IAggregateRoot, IOwnedResource
{
    private readonly List<ReservationItem> _items = [];

    private Reservation() => IdempotencyKey = string.Empty;

    public Reservation(
        Guid userId,
        Guid eventSessionId,
        string idempotencyKey,
        IReadOnlyList<ReservationLine> lines,
        DateTime utcNow,
        TimeSpan lockDuration,
        int maxSeatsPerSession,
        ServiceFeePolicy serviceFee)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (userId == Guid.Empty)
            throw new DomainException("Rezervasyon bir kullaniciya bagli olmali.");

        if (eventSessionId == Guid.Empty)
            throw new DomainException("Rezervasyon bir oturuma bagli olmali.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency anahtari zorunludur.");

        if (lockDuration <= TimeSpan.Zero)
            throw new DomainException("Kilit suresi sifirdan buyuk olmali.");

        if (lines.Count == 0)
            throw new DomainException("En az bir koltuk secilmeli.");

        // TEK ISTEKTEKI koltuk sayisi. Kullanicinin ayni oturumdaki DIGER
        // rezervasyonlarini da sayan kontrol handler'da: o karar bu
        // aggregate'in gormedigi satirlari gerektiriyor. Ayni bolunme
        // Event.AddSession ile salon cakismasi kontrolunde de var.
        if (lines.Count > maxSeatsPerSession)
        {
            throw new DomainException(
                $"Bir oturumda en fazla {maxSeatsPerSession} koltuk secilebilir.");
        }

        // Ayni koltugun iki kez gonderilmesi. Engellenmeseydi kullanici
        // limiti asmadan ayni koltuk icin iki kalem ve iki kat tutar olurdu.
        if (lines.Select(line => line.EventSeatId).Distinct().Count() != lines.Count)
            throw new DomainException("Ayni koltuk birden fazla kez secilemez.");

        UserId = userId;
        EventSessionId = eventSessionId;
        IdempotencyKey = idempotencyKey.Trim();
        ExpiresAtUtc = utcNow.Add(lockDuration);

        // Toplam sifirdan baslayip kalemlerle birikiyor. Money farkli para
        // birimlerinin toplanmasina zaten izin vermiyor; karisik para birimli
        // bir istek burada DomainException'a duser.
        var toplam = Money.Zero(lines[0].Price.Currency);

        foreach (var line in lines)
        {
            _items.Add(new ReservationItem(Id, line.EventSeatId, line.TicketTypeId, line.Price));
            toplam += line.Price;
        }

        // Hizmet bedeli kalemler toplandiktan SONRA hesaplaniyor: yuzde
        // toplamin uzerinden isliyor ve kalem kalem hesaplanip toplansaydi
        // her kalemde ayri bir yuvarlama olur, toplam birkac kurus kayardi.
        ServiceFee = serviceFee.Calculate(toplam, lines.Count);
        TotalAmount = toplam + ServiceFee;
    }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public Guid EventSessionId { get; private set; }
    public EventSession? EventSession { get; private set; }

    public ReservationStatus Status { get; private set; } = ReservationStatus.Pending;

    /// <summary>Kilidin dusecegi an. Uzatma bu degeri oteler.</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Uzatma hakki kullanildi mi. Sartname: en fazla bir kez.</summary>
    public bool ExtensionUsed { get; private set; }

    /// <summary>
    /// Tahsil edilecek toplam: koltuk fiyatlari + hizmet bedeli.
    /// </summary>
    /// <remarks>
    /// Sunucuda hesaplanir, istekten alinmaz. <b>Odeme bu tutari
    /// kopyaliyor</b>, kalemleri degil.
    /// </remarks>
    public Money TotalAmount { get; private set; }

    /// <summary>
    /// Platformun payi. Bilet fiyatinin USTUNE eklenir.
    /// </summary>
    /// <remarks>
    /// Ayri alan olarak saklaniyor, toplamdan cikarilarak turetilmiyor:
    /// politika sonradan degistiginde eski rezervasyonlarin o gunku bedeli
    /// olduğu gibi kalmali. Turetilseydi gecmise donuk bir raporda
    /// bugunku yuzdeyle hesaplanmis yanlis rakamlar cikardi.
    /// </remarks>
    public Money ServiceFee { get; private set; }

    /// <summary>Hizmet bedeli haric, koltuklarin toplami.</summary>
    /// <remarks>
    /// Saklanmiyor, hesaplaniyor: iki tutar da saklanip ucuncusu
    /// turetilseydi uc degerin birbirini tutmama ihtimali dogardi.
    /// </remarks>
    public Money ItemsTotal => TotalAmount - ServiceFee;

    /// <summary>
    /// Istegi tekilleyen anahtar.
    /// </summary>
    /// <remarks>
    /// Istemci <c>Idempotency-Key</c> basligini gonderir. Ag koptugunda veya
    /// kullanici iki kez tikladiginda ayni anahtarla gelen ikinci istek yeni
    /// rezervasyon acmaz, ilkinin sonucunu doner. Olmasaydi tek tiklama
    /// tekrarinda kullanici ayni koltuklar icin iki kez odeme yapardi.
    /// Tekillik <c>UNIQUE(UserId, IdempotencyKey)</c> ile veritabaninda da
    /// zorlanir; uygulama katmanindaki kontrol yarisi kaybedebilir.
    /// </remarks>
    public string IdempotencyKey { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    public IReadOnlyList<ReservationItem> Items => _items;

    public int SeatCount => _items.Count;

    Guid IOwnedResource.OwnerId => UserId;

    /// <summary>
    /// Suresi dolmus mu.
    /// </summary>
    /// <remarks>
    /// Durum alani degil hesaplanan bir deger: arka plan isi calismadan once
    /// de sure dolmus olabilir. Yalnizca <c>Status</c>'e bakilsaydi is
    /// calisana kadar gecen saniyelerde suresi dolmus bir rezervasyonun
    /// odemesi alinabilirdi.
    /// </remarks>
    public bool IsExpired(DateTime utcNow) =>
        Status == ReservationStatus.Pending && utcNow >= ExpiresAtUtc;

    /// <summary>Koltuklari hâlâ tutuyor mu.</summary>
    public bool IsActive(DateTime utcNow) =>
        Status == ReservationStatus.Confirmed ||
        (Status == ReservationStatus.Pending && !IsExpired(utcNow));

    /// <summary>Kilidin bitmesine kalan sure. Dolmussa sifir.</summary>
    public TimeSpan RemainingTime(DateTime utcNow) =>
        ExpiresAtUtc > utcNow ? ExpiresAtUtc - utcNow : TimeSpan.Zero;

    /// <summary>
    /// Kilit suresini bir kez uzatir.
    /// </summary>
    /// <remarks>
    /// Sinirsiz uzatma, koltugu satin almadan istedigi kadar tutmak demek
    /// olurdu; bir seyirci tum salonu kilitleyip satisi durdurabilirdi.
    /// </remarks>
    /// <returns>Yeni bitis ani.</returns>
    public DateTime Extend(DateTime utcNow, TimeSpan extension)
    {
        if (Status != ReservationStatus.Pending)
            throw new DomainException($"Bu durumdaki rezervasyon uzatilamaz: {Status}.");

        // Suresi dolmus kilit uzatilmaz: koltuklar bu arada baskasina
        // gitmis olabilir. Uzatilsaydi rezervasyon, artik tutmadigi
        // koltuklarla ayakta kalirdi.
        if (IsExpired(utcNow))
            throw new DomainException("Suresi dolmus rezervasyon uzatilamaz.");

        if (ExtensionUsed)
            throw new DomainException("Uzatma hakki yalnizca bir kez kullanilabilir.");

        if (extension <= TimeSpan.Zero)
            throw new DomainException("Uzatma suresi sifirdan buyuk olmali.");

        ExpiresAtUtc = ExpiresAtUtc.Add(extension);
        ExtensionUsed = true;

        return ExpiresAtUtc;
    }

    /// <summary>
    /// Havale ile odeme secildi; kilit banka saatlerine uygun bir pencereye cekilir.
    /// </summary>
    /// <remarks>
    /// <b><see cref="Extend"/>'den ayri bir metot, kopya degil.</b> Uzatma
    /// kullanicinin bir kerelik nezaket hakki (dakikalar); bu ise odeme
    /// yonteminin gerektirdigi pencere (saatler). Ayni metot kullanilsaydi
    /// havale secen kullanici uzatma hakkini da harcamis olurdu ve on
    /// dakikalik kilide bes dakika eklemek havaleye hicbir sey kazandirmazdi:
    /// banka saatlerine bagli bir odeme on bes dakikada tamamlanamaz.
    ///
    /// <para>
    /// Pencere <b>kisaltmiyor</b>: mevcut bitis daha ileridiyse dokunulmuyor.
    /// Bu sure boyunca koltuk odenmemis hâlde tutuluyor — isletme riski, bu
    /// yuzden suresi koda gomulu degil panelden ayarlaniyor.
    /// </para>
    ///
    /// <para>
    /// Rezervasyon basina bir kez isliyor: ayni rezervasyon icin ikinci bir
    /// bekleyen odeme acilamiyor, reddedilen havale rezervasyonu iptal
    /// ediyor. Yani pencere tekrar tekrar uzatilarak koltuk suresiz
    /// tutulamiyor.
    /// </para>
    /// </remarks>
    public void ExtendForBankTransfer(DateTime utcNow, TimeSpan window)
    {
        if (Status != ReservationStatus.Pending)
            throw new DomainException($"Bu durumdaki rezervasyon havaleye acilamaz: {Status}.");

        if (IsExpired(utcNow))
            throw new DomainException("Suresi dolmus rezervasyon havaleye acilamaz.");

        if (window <= TimeSpan.Zero)
            throw new DomainException("Havale suresi sifirdan buyuk olmali.");

        var yeniBitis = utcNow.Add(window);

        if (yeniBitis > ExpiresAtUtc)
            ExpiresAtUtc = yeniBitis;
    }

    /// <summary>Kullanici vazgecti veya odeme basarisiz oldu.</summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status == ReservationStatus.Confirmed)
        {
            // Odemesi alinmis rezervasyonun iptali iade akisi gerektiriyor;
            // durumu burada degistirmek parayi kullanicida birakmadan
            // koltugu geri verirdi. Gun 7'nin isi.
            throw new DomainException(
                "Odemesi tamamlanmis rezervasyon buradan iptal edilemez; iade akisi gerekir.");
        }

        if (Status != ReservationStatus.Pending)
            throw new DomainException($"Bu durumdaki rezervasyon iptal edilemez: {Status}.");

        Status = ReservationStatus.Cancelled;
        CancelledAtUtc = utcNow;
    }

    /// <summary>
    /// Odemesi iade edildi; rezervasyon kapanir.
    /// </summary>
    /// <remarks>
    /// <see cref="Cancel"/> onaylanmis rezervasyonu bilerek reddediyor
    /// ("iade akisi gerekir"). Iade o akisin kendisi: para geri gonderildikten
    /// SONRA cagriliyor, dolayisiyla koltugu geri vermek artik dogru.
    /// </remarks>
    public void MarkRefunded(DateTime utcNow)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException($"Bu durumdaki rezervasyon iade edilemez: {Status}.");

        Status = ReservationStatus.Cancelled;
        CancelledAtUtc = utcNow;
    }

    /// <summary>Sure doldu; koltuklar geri birakilacak.</summary>
    public void Expire()
    {
        if (Status != ReservationStatus.Pending)
            throw new DomainException($"Bu durumdaki rezervasyonun suresi doldurulamaz: {Status}.");

        Status = ReservationStatus.Expired;
    }

    /// <summary>Odeme tamamlandi (Gun 7).</summary>
    public void Confirm(DateTime utcNow)
    {
        if (Status != ReservationStatus.Pending)
            throw new DomainException($"Bu durumdaki rezervasyonun odemesi alinamaz: {Status}.");

        if (IsExpired(utcNow))
            throw new DomainException("Suresi dolmus rezervasyonun odemesi alinamaz.");

        Status = ReservationStatus.Confirmed;
        ConfirmedAtUtc = utcNow;
    }
}
