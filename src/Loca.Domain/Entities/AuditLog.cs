using Loca.Domain.Common;

namespace Loca.Domain.Entities;

/// <summary>
/// Is anlamli bir degisikligin denetim kaydi.
/// </summary>
/// <remarks>
/// <see cref="BaseEntity"/> uzerindeki <c>CreatedBy</c> / <c>UpdatedBy</c>
/// alanlari "en son kim dokundu" sorusunu cevaplar ama <b>eski degeri</b>
/// tutmaz. Sartnamenin istedigi "satisi baslamis bilet turunun fiyati
/// degistirilirse loglanmalidir" kurali icin eski ve yeni degerin ikisi de
/// gerekli — o yuzden ayri bir tablo.
///
/// <para>
/// Kayit degismez: guncelleme veya silme metodu yok. Denetim kaydinin
/// sonradan duzeltilebilmesi denetim olmaktan cikarirdi.
/// </para>
/// </remarks>
public sealed class AuditLog : BaseEntity
{
    private AuditLog()
    {
        EntityName = string.Empty;
        Action = string.Empty;
    }

    public AuditLog(
        string entityName,
        Guid entityId,
        string action,
        DateTime occurredAtUtc,
        string? oldValues = null,
        string? newValues = null,
        Guid? userId = null,
        string? correlationId = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            throw new DomainException("Denetim kaydinda varlik adi zorunlu.");

        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Denetim kaydinda islem adi zorunlu.");

        EntityName = entityName.Trim();
        EntityId = entityId;
        Action = action.Trim();
        OccurredAtUtc = occurredAtUtc;
        OldValues = oldValues;
        NewValues = newValues;
        UserId = userId;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
    }

    /// <summary>Degisen varligin tip adi: "TicketType".</summary>
    public string EntityName { get; private set; }

    public Guid EntityId { get; private set; }

    /// <summary>Islem: Created / Updated / Deleted / PriceChanged.</summary>
    public string Action { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Degisiklikten onceki hâl (jsonb).</summary>
    public string? OldValues { get; private set; }

    /// <summary>Degisiklikten sonraki hâl (jsonb).</summary>
    public string? NewValues { get; private set; }

    /// <summary>Islemi yapan. Arka plan islerinde bos olabilir.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Istek zincirini loglarla eslestirmek icin.</summary>
    public string? CorrelationId { get; private set; }

    public string? IpAddress { get; private set; }
}

/// <summary>Denetim kaydi islem adlari. Serbest metin yerine sabit.</summary>
/// <remarks>
/// Elle yazilsaydi "PriceChanged", "priceChanged" ve "Price changed" ayri
/// degerler olarak birikir ve denetim ekranindaki filtre calismazdi.
/// </remarks>
public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string PriceChanged = "PriceChanged";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
}
