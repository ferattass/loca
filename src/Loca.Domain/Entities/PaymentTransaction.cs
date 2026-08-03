using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Odeme uzerindeki tek bir saglayici etkilesimi.
/// </summary>
/// <remarks>
/// <see cref="Payment.Status"/> yalnizca son hâli tasir. "Kac kez denendi,
/// saglayici ne dondu, hangi denemede basarisiz oldu" sorularinin cevabi tek
/// bir durum alanina sigmaz; her etkilesim buraya bir satir olarak yaziliyor.
/// Kayitlar hicbir zaman silinmiyor veya guncellenmiyor — sonradan
/// duzeltilebilen bir denetim izi denetim izi degildir.
/// </remarks>
public sealed class PaymentTransaction : BaseEntity
{
    private PaymentTransaction() { }

    /// <remarks>
    /// <c>internal</c>: satir yalnizca <see cref="Payment"/> icinden
    /// olusturulur. Disaridan eklenebilseydi odemenin durumu ile izi
    /// birbirini tutmayabilirdi.
    /// </remarks>
    internal PaymentTransaction(
        Guid paymentId,
        PaymentTransactionType type,
        DateTime occurredAtUtc,
        string? providerReference,
        string? message)
    {
        if (paymentId == Guid.Empty)
            throw new DomainException("Islem bir odemeye bagli olmali.");

        PaymentId = paymentId;
        Type = type;
        OccurredAtUtc = occurredAtUtc;
        ProviderReference = providerReference;
        Message = message;
    }

    public Guid PaymentId { get; private set; }
    public Payment? Payment { get; private set; }

    public PaymentTransactionType Type { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Saglayicinin islem kimligi. Mutabakat icin.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>
    /// Saglayicidan donen aciklama veya hata sebebi.
    /// </summary>
    /// <remarks>
    /// <b>Kart bilgisi ASLA buraya yazilmaz.</b> Sartname kart verisi
    /// saklamayi kapsam disi birakiyor; saglayici cevabi ham hâliyle
    /// tasinsaydi kart numarasinin son haneleri veya token'i denetim
    /// tablosuna sizardi.
    /// </remarks>
    public string? Message { get; private set; }
}
