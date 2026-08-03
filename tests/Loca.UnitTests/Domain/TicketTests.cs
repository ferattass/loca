using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>Biletin durum gecisleri.</summary>
public class TicketTests
{
    private static readonly DateTime Simdi = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket Yeni() =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "LOCA-7K3M-9P2Q",
            "qr-kodu-degeri",
            "Bir Yaz Gecesi",
            "Orta A-12",
            "Tam",
            new DateTime(2027, 3, 15, 20, 0, 0, DateTimeKind.Utc),
            new Money(450, "TRY"),
            Simdi);

    [Fact]
    public void NewTicketIsValid()
    {
        var bilet = Yeni();

        Assert.Equal(TicketStatus.Valid, bilet.Status);
        Assert.Equal(Simdi, bilet.IssuedAtUtc);
        Assert.Null(bilet.UsedAtUtc);
    }

    [Fact]
    public void TicketNumberAndQrAreRequired()
    {
        Assert.Throws<DomainException>(() =>
            new Ticket(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                Guid.CreateVersion7(), "  ", "qr", "E", "A-1", "Tam",
                Simdi, new Money(1, "TRY"), Simdi));
    }

    /// <remarks>
    /// Ayni QR ile iki kisinin girmesi, bileti ekran goruntusu olarak
    /// paylasmakla mumkun olurdu.
    /// </remarks>
    [Fact]
    public void TicketCannotBeUsedTwice()
    {
        var bilet = Yeni();
        bilet.MarkUsed(Simdi);

        Assert.Equal(TicketStatus.Used, bilet.Status);
        Assert.Throws<DomainException>(() => bilet.MarkUsed(Simdi.AddMinutes(1)));
    }

    [Fact]
    public void CancelledTicketCannotBeUsed()
    {
        var bilet = Yeni();
        bilet.Cancel(Simdi);

        Assert.Throws<DomainException>(() => bilet.MarkUsed(Simdi.AddMinutes(1)));
    }

    /// <remarks>
    /// Etkinlige girmis bir seyircinin bedeli iade edilemez.
    /// </remarks>
    [Fact]
    public void UsedTicketCannotBeRefunded()
    {
        var bilet = Yeni();
        bilet.MarkUsed(Simdi);

        Assert.Throws<DomainException>(() => bilet.MarkRefunded(Simdi.AddHours(1)));
    }

    [Fact]
    public void CancelAndRefundAreIdempotent()
    {
        var bilet = Yeni();

        bilet.Cancel(Simdi);
        bilet.Cancel(Simdi.AddMinutes(1));
        Assert.Equal(TicketStatus.Cancelled, bilet.Status);

        var digeri = Yeni();
        digeri.MarkRefunded(Simdi);
        digeri.MarkRefunded(Simdi.AddMinutes(1));
        Assert.Equal(TicketStatus.Refunded, digeri.Status);
    }

    /// <remarks>
    /// Bilet bir belgedir: kesildigi andaki bilgileri tasir. Etkinligin adi
    /// sonradan degistiginde gecmis bilet degismemeli.
    /// </remarks>
    [Fact]
    public void TicketKeepsIssueTimeSnapshot()
    {
        var bilet = Yeni();

        Assert.Equal("Bir Yaz Gecesi", bilet.EventTitle);
        Assert.Equal("Orta A-12", bilet.SeatLabel);
        Assert.Equal("Tam", bilet.TicketTypeName);
        Assert.Equal(450m, bilet.Price.Amount);
    }
}
