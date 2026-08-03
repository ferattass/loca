using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Odemenin durum makinesi ve callback idempotency'si.
/// </summary>
/// <remarks>
/// Buradaki bir hata "para alindi ama bilet cikmadi" veya "ayni odeme iki
/// kez bilet uretti" demek; gecisler tek tek testle sabitlendi.
/// </remarks>
public class PaymentTests
{
    private static readonly DateTime Simdi = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Payment Yeni(decimal tutar = 900m) =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new Money(tutar, "TRY"),
            "Mock",
            "anahtar-1",
            Simdi);

    [Fact]
    public void NewPaymentIsPendingWithCreateTransaction()
    {
        var odeme = Yeni();

        Assert.Equal(PaymentStatus.Pending, odeme.Status);
        Assert.True(odeme.IsPending);
        Assert.False(odeme.IsSuccessful);

        // Denemelerin dokumu ilk andan itibaren tutuluyor.
        Assert.Single(odeme.Transactions);
        Assert.Equal(PaymentTransactionType.Create, odeme.Transactions[0].Type);
    }

    [Fact]
    public void ZeroAmountIsRejected() =>
        Assert.Throws<DomainException>(() => Yeni(0m));

    /// <remarks>
    /// Saglayicilar ayni bildirimi birden fazla kez gonderir. Ikinci cagri
    /// hata DEGIL; durum degismedigi icin <c>false</c> donuyor ve cagiran
    /// taraf bileti ikinci kez uretmiyor.
    /// </remarks>
    [Fact]
    public void SecondCompleteIsIdempotent()
    {
        var odeme = Yeni();

        Assert.True(odeme.Complete("REF-1", Simdi));
        Assert.False(odeme.Complete("REF-1", Simdi.AddSeconds(5)));

        Assert.Equal(PaymentStatus.Succeeded, odeme.Status);
        Assert.Equal(Simdi, odeme.CompletedAtUtc);

        // Ikinci bildirim yeni bir islem satiri da yazmiyor.
        Assert.Equal(2, odeme.Transactions.Count);
    }

    /// <remarks>
    /// Gec gelen bir bildirim ilk basarili sonucun kimligini degistirmemeli;
    /// degistirseydi mutabakat kaydi bozulurdu.
    /// </remarks>
    [Fact]
    public void SecondCompleteDoesNotOverwriteReference()
    {
        var odeme = Yeni();
        odeme.Complete("REF-ILK", Simdi);

        odeme.Complete("REF-SONRA", Simdi.AddMinutes(1));

        Assert.Equal("REF-ILK", odeme.ProviderReference);
    }

    [Fact]
    public void SecondFailIsIdempotent()
    {
        var odeme = Yeni();

        Assert.True(odeme.Fail("Yetersiz bakiye", Simdi));
        Assert.False(odeme.Fail("Yetersiz bakiye", Simdi.AddSeconds(3)));

        Assert.Equal(PaymentStatus.Failed, odeme.Status);
    }

    /// <remarks>
    /// Gec gelen bir "reddedildi" bildirimi, parasi alinmis ve bileti
    /// uretilmis bir rezervasyonu iptal edemez.
    /// </remarks>
    [Fact]
    public void SucceededPaymentCannotBeFailed()
    {
        var odeme = Yeni();
        odeme.Complete("REF-1", Simdi);

        Assert.Throws<DomainException>(() => odeme.Fail("Gec gelen ret", Simdi.AddMinutes(5)));
    }

    [Fact]
    public void FailedPaymentCannotBeCompleted()
    {
        var odeme = Yeni();
        odeme.Fail("Yetersiz bakiye", Simdi);

        Assert.Throws<DomainException>(() => odeme.Complete("REF-1", Simdi.AddMinutes(1)));
    }

    [Fact]
    public void OnlySucceededPaymentCanBeRefunded()
    {
        var bekleyen = Yeni();
        Assert.Throws<DomainException>(() => bekleyen.Refund(Simdi));

        var basarili = Yeni();
        basarili.Complete("REF-1", Simdi);

        Assert.True(basarili.Refund(Simdi.AddHours(1)));
        Assert.Equal(PaymentStatus.Refunded, basarili.Status);

        // Ikinci iade cagrisi da sessizce gecerli: tekrar eden bildirim.
        Assert.False(basarili.Refund(Simdi.AddHours(2)));
    }

    [Fact]
    public void CancelOnlyFromPending()
    {
        var odeme = Yeni();
        Assert.True(odeme.Cancel(Simdi));
        Assert.Equal(PaymentStatus.Cancelled, odeme.Status);

        var basarili = Yeni();
        basarili.Complete("REF-1", Simdi);

        Assert.Throws<DomainException>(() => basarili.Cancel(Simdi.AddMinutes(1)));
    }

    /// <remarks>
    /// Saglayici kimligi yalnizca sonuclanmamis odemede yazilabilir;
    /// tamamlanmis bir odemenin kimligini degistirmek mutabakati bozardi.
    /// </remarks>
    [Fact]
    public void ProviderReferenceOnlyWhilePending()
    {
        var odeme = Yeni();
        odeme.AttachProviderReference("REF-1");
        Assert.Equal("REF-1", odeme.ProviderReference);

        odeme.Complete("REF-1", Simdi);

        Assert.Throws<DomainException>(() => odeme.AttachProviderReference("REF-2"));
    }

    [Fact]
    public void EveryTransitionIsRecorded()
    {
        var odeme = Yeni();
        odeme.Complete("REF-1", Simdi);
        odeme.Refund(Simdi.AddHours(1), "Etkinlik iptal edildi");

        var turler = odeme.Transactions.Select(islem => islem.Type).ToList();

        Assert.Equal(
            [PaymentTransactionType.Create, PaymentTransactionType.Complete, PaymentTransactionType.Refund],
            turler);
    }
}

/// <summary>Outbox mesajinin islenme kurallari.</summary>
public class OutboxMessageTests
{
    private static readonly DateTime Simdi = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    private static OutboxMessage Yeni() => new("TicketsIssued", "{\"a\":1}", Simdi);

    [Fact]
    public void NewMessageIsPending()
    {
        var mesaj = Yeni();

        Assert.False(mesaj.IsProcessed);
        Assert.True(mesaj.CanRetry);
        Assert.False(mesaj.IsDeadLettered);
    }

    [Fact]
    public void EmptyPayloadIsRejected() =>
        Assert.Throws<DomainException>(() => new OutboxMessage("Tur", "  ", Simdi));

    /// <remarks>
    /// Ayni kaydin iki kez islenmesini engelleyen kural. Ikinci cagri hata
    /// firlatmiyor: isi yapan taraf ayni mesaji tekrar gorebilir.
    /// </remarks>
    [Fact]
    public void MarkProcessedIsIdempotent()
    {
        var mesaj = Yeni();

        mesaj.MarkProcessed(Simdi);
        var ilk = mesaj.ProcessedAtUtc;

        mesaj.MarkProcessed(Simdi.AddMinutes(5));

        Assert.Equal(ilk, mesaj.ProcessedAtUtc);
    }

    [Fact]
    public void RetryCountRunsOutAndMessageIsDeadLettered()
    {
        var mesaj = Yeni();

        for (var i = 0; i < OutboxMessage.MaxRetryCount; i++)
            mesaj.MarkFailed("SMTP baglantisi kurulamadi");

        Assert.False(mesaj.CanRetry);
        Assert.True(mesaj.IsDeadLettered);
        Assert.Equal(OutboxMessage.MaxRetryCount, mesaj.RetryCount);
    }

    [Fact]
    public void ProcessedMessageCannotFail()
    {
        var mesaj = Yeni();
        mesaj.MarkProcessed(Simdi);

        Assert.Throws<DomainException>(() => mesaj.MarkFailed("hata"));
    }

    /// <remarks>
    /// Saglayici bazen tum stack trace'i doner; tablo sismesin diye kirpiliyor.
    /// </remarks>
    [Fact]
    public void ErrorMessageIsTruncated()
    {
        var mesaj = Yeni();
        mesaj.MarkFailed(new string('x', 5000));

        Assert.Equal(1000, mesaj.ErrorMessage!.Length);
    }
}
