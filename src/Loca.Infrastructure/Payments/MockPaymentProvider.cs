using System.Globalization;
using Loca.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loca.Infrastructure.Payments;

/// <summary>
/// Her zaman basarili olan taklit saglayici.
/// </summary>
/// <remarks>
/// Sartname gercek odeme saglayicisini kapsam disi birakiyor. Bu uygulama
/// gercek bir saglayicinin gozlemlenebilir davranisini taklit ediyor:
/// gecikme, islem kimligi ve dogrulama cagrisi. Boylece odeme akisi gercek
/// entegrasyonda degismeyecek.
///
/// <para>
/// <b>Kart bilgisi almiyor ve saklamiyor.</b> Gercek entegrasyonda da
/// kart verisi sunucudan gecmemeli.
/// </para>
/// </remarks>
internal sealed class MockPaymentProvider(
    PaymentOptions options,
    ILogger<MockPaymentProvider> logger) : IPaymentService
{
    public string Name => "Mock";

    public async Task<PaymentResult> CreatePaymentAsync(
        Guid paymentId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        await GecikmeyiTaklitEt(cancellationToken);

        var referans = Referans(paymentId);

        logger.LogInformation(
            "Taklit odeme baslatildi. OdemeId: {OdemeId}, Tutar: {Tutar} {Birim}, Referans: {Referans}",
            paymentId,
            amount,
            currency,
            referans);

        return PaymentResult.Success(referans);
    }

    public async Task<PaymentResult> VerifyPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default)
    {
        await GecikmeyiTaklitEt(cancellationToken);

        return PaymentResult.Success(reference ?? Referans(paymentId));
    }

    public async Task<PaymentResult> RefundPaymentAsync(
        Guid paymentId, string? reference, decimal amount, CancellationToken cancellationToken = default)
    {
        await GecikmeyiTaklitEt(cancellationToken);

        logger.LogInformation(
            "Taklit iade. OdemeId: {OdemeId}, Tutar: {Tutar}", paymentId, amount);

        return PaymentResult.Success(reference ?? Referans(paymentId));
    }

    public async Task<PaymentResult> CancelPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default)
    {
        await GecikmeyiTaklitEt(cancellationToken);

        return PaymentResult.Success(reference ?? Referans(paymentId));
    }

    /// <summary>
    /// Ayni odeme icin HER ZAMAN ayni referansi uretir.
    /// </summary>
    /// <remarks>
    /// Rastgele uretilseydi, tekrar eden bir dogrulama cagrisi farkli bir
    /// kimlik dondurur ve mutabakat imkansiz hâle gelirdi. Gercek saglayicilar
    /// da islem kimligini degistirmez.
    /// </remarks>
    private static string Referans(Guid paymentId) =>
        string.Create(CultureInfo.InvariantCulture, $"MOCK-{paymentId:N}");

    private Task GecikmeyiTaklitEt(CancellationToken cancellationToken) =>
        options.SimulatedLatencyMs > 0
            ? Task.Delay(options.SimulatedLatencyMs, cancellationToken)
            : Task.CompletedTask;
}

/// <summary>
/// Her zaman basarisiz olan taklit saglayici.
/// </summary>
/// <remarks>
/// Yalnizca yapilandirmayla secilir. Varligi bir test kolayligindan ibaret
/// degil: "odeme basarisiz olursa koltuklar serbest kaliyor mu, bilet
/// uretilmiyor mu" kurallari ancak basarisiz bir odemeyle dogrulanabilir ve
/// bunu gercek bir saglayici hatasi bekleyerek yapmak mumkun degil.
/// </remarks>
internal sealed class FailedPaymentProvider(ILogger<FailedPaymentProvider> logger) : IPaymentService
{
    private const string Sebep = "Taklit saglayici: odeme reddedildi.";

    public string Name => "FailedMock";

    public Task<PaymentResult> CreatePaymentAsync(
        Guid paymentId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        // Baslatma BASARILI: gercek hayatta da islem acilir, sonra reddedilir.
        // Baslatmada basarisiz olsaydi "odeme baslatildi ama tamamlanmadi"
        // durumu hic olusmaz ve o yol test edilmemis kalirdi.
        logger.LogInformation("Basarisiz taklit saglayici, odeme baslatildi. OdemeId: {OdemeId}", paymentId);

        return Task.FromResult(PaymentResult.Success($"FAIL-{paymentId:N}"));
    }

    public Task<PaymentResult> VerifyPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(PaymentResult.Failure(Sebep));

    public Task<PaymentResult> RefundPaymentAsync(
        Guid paymentId, string? reference, decimal amount, CancellationToken cancellationToken = default) =>
        Task.FromResult(PaymentResult.Failure(Sebep));

    public Task<PaymentResult> CancelPaymentAsync(
        Guid paymentId, string? reference, CancellationToken cancellationToken = default) =>
        // Iptal edilen odeme icin saglayici kimligi olmayabilir: islem hic
        // acilmadan da iptal edilebilir.
        Task.FromResult(PaymentResult.Success(reference ?? $"FAIL-{paymentId:N}"));
}
