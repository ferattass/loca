using System.ComponentModel.DataAnnotations;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Hangi odeme saglayicisinin kullanilacagi.
/// </summary>
/// <remarks>
/// <c>FailedMock</c> secenegi test icin: "odeme basarisiz olursa koltuklar
/// serbest kaliyor mu" senaryosunu gercek bir saglayici hatasi beklemeden
/// calistirmayi sagliyor. Uretimde gercek saglayici gelecek.
/// </remarks>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>Gecerli degerler: <c>Mock</c>, <c>FailedMock</c>.</summary>
    [Required]
    public string Provider { get; set; } = "Mock";

    /// <summary>
    /// Taklit saglayicinin cevap suresi (ms).
    /// </summary>
    /// <remarks>
    /// Sifir olsaydi odeme aninda tamamlanir ve "odeme surerken koltugun
    /// durumu ne" sorusu hic test edilemezdi.
    /// </remarks>
    [Range(0, 5000)]
    public int SimulatedLatencyMs { get; set; } = 150;
}
