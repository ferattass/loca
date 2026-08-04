using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Is islemiyle AYNI transaction'da yazilan, sonra ayri islenecek mesaj.
/// </summary>
/// <remarks>
/// <b>Neden var:</b> odeme basarili olduktan sonra bildirim gondermek
/// gerekiyor. Bildirim transaction icinde gonderilseydi, e-posta sunucusu
/// yavas oldugunda veritabani transaction'i acik kalir; hata verdiginde ise
/// tamamlanmis bir odeme geri alinirdi. Transaction'dan SONRA gonderilseydi
/// bu kez arada olusan bir cokme bildirimi tamamen kaybettirirdi.
///
/// <para>
/// Cozum: mesaj is verisiyle ayni transaction'da tabloya yazilir. Ya ikisi
/// birden olur ya hicbiri. Gonderim isini ayri bir is (job) ustlenir.
/// </para>
///
/// <para>
/// <b>En az bir kez teslim.</b> Isleyen taraf mesaji gonderip
/// <see cref="MarkProcessed"/> cagirmadan cokerse mesaj tekrar islenir; bu
/// yuzden mesaji tuketen tarafin islemi tekrarlanabilir olmasi gerekir.
/// "Tam bir kez" garantisi dagitik sistemlerde tek tablo ile saglanamaz.
/// </para>
/// </remarks>
public sealed class OutboxMessage : BaseEntity
{
    /// <summary>Bu sayida denemeden sonra mesaj olu mektup sayilir.</summary>
    public const int MaxRetryCount = 5;

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(string type, string payload, DateTime utcNow, Guid? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("Mesaj turu bos olamaz.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainException("Mesaj govdesi bos olamaz.");

        Type = type.Trim();
        Payload = payload;
        OccurredAtUtc = utcNow;
        CorrelationId = correlationId;
    }

    /// <summary>Olayin adi: <c>ReservationConfirmed</c>, <c>TicketsIssued</c>.</summary>
    public string Type { get; private set; }

    /// <summary>Olayin govdesi (JSON).</summary>
    public string Payload { get; private set; }

    /// <summary>Olayin gerceklestigi an; yazildigi an degil.</summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Islendigi an. <c>null</c> ise bekliyor.
    /// </summary>
    /// <remarks>
    /// Ayni kaydin iki kez islenmesini engelleyen alan. Silme yerine
    /// isaretleme yapiliyor: islenmis mesajlar bir sure duruyor, cunku
    /// "bu bildirim gitti mi" sorusunun cevabi olay sonrasi arastirmada
    /// gerekiyor.
    /// </remarks>
    public DateTime? ProcessedAtUtc { get; private set; }

    public int RetryCount { get; private set; }

    /// <summary>Son basarisiz denemenin hatasi.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Ilgili kaydin kimligi (rezervasyon, odeme). Iz surmek icin.</summary>
    public Guid? CorrelationId { get; private set; }

    public bool IsProcessed => ProcessedAtUtc is not null;

    /// <summary>Yeniden denenebilir mi; deneme hakki tukendi mi.</summary>
    public bool CanRetry => !IsProcessed && RetryCount < MaxRetryCount;

    /// <summary>
    /// Deneme hakki bitmis ve hâlâ islenmemis.
    /// </summary>
    /// <remarks>
    /// Bu kayitlar sessizce kaybolmamali: sonsuza kadar denenirse kuyruk
    /// tikanir, silinirse hata gorunmez olur. Isaretli birakilip raporlaniyor.
    /// </remarks>
    public bool IsDeadLettered => !IsProcessed && RetryCount >= MaxRetryCount;

    public void MarkProcessed(DateTime utcNow)
    {
        if (IsProcessed)
            return;

        ProcessedAtUtc = utcNow;
        ErrorMessage = null;
    }

    /// <summary>Deneme basarisiz oldu; sayaci artirir ve hatayi kaydeder.</summary>
    public void MarkFailed(string error)
    {
        if (IsProcessed)
            throw new DomainException("Islenmis mesaj basarisiz olarak isaretlenemez.");

        RetryCount++;

        // Hata metni kirpiliyor: saglayici bazen tum stack trace'i doner ve
        // tablo gereksiz yere sisirdi.
        ErrorMessage = error.Length > 1000 ? error[..1000] : error;
    }

    /// <summary>Olu mektubu kuyruga geri koyar.</summary>
    /// <remarks>
    /// <b>Elle tetiklenir, kendiliginden olmaz.</b> Hakki tuketen mesaj bir
    /// sebeple basarisiz oldu; sebep giderilmeden geri konursa yeniden
    /// tukenir ve kuyruk bosuna calisir. Karari veren kisi, sorunun
    /// cozuldugunu bilen kisi olmali.
    ///
    /// <para>
    /// Deneme sayaci sifirlanmiyor, <b>bire</b> cekiliyor: kaydin daha once
    /// basarisiz oldugu bilgisi kaybolmamali, mesaj "yeniden denenecek"
    /// kumesine dusmeli ki ilk kez gonderilen mesajlarin sirasini isgal
    /// etmesin.
    /// </para>
    /// </remarks>
    public void RequeueFromDeadLetter()
    {
        if (IsProcessed)
            throw new DomainException("Islenmis mesaj kuyruga geri konamaz.");

        if (!IsDeadLettered)
            throw new DomainException("Yalnizca deneme hakki tukenmis mesaj geri konabilir.");

        RetryCount = 1;
    }
}
