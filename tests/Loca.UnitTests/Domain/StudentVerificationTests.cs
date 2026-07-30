using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Ogrenci dogrulamasinda kimlik numarasinin opsiyonel olmasi.
/// </summary>
/// <remarks>
/// <b>Kuralin ozu:</b> yabanci uyruklu ogrencinin T.C. kimlik numarasi
/// olmaz. Alan zorunlu yapilirsa bu ogrenciler ogrenci bileti alamaz.
/// Numara yoksa ogrenciyi tanimlayan deger ogrenci numarasina duser ve
/// kayit REDDEDILMEZ.
///
/// <para>
/// Buradaki testler kuralin sessizce geri alinmasini engelliyor: biri
/// <c>NationalIdentityNumber</c> alanini zorunlu yaparsa
/// <see cref="ShouldBeAcceptedWithoutNationalIdentity"/> kirilir.
/// </para>
/// </remarks>
public class StudentVerificationTests
{
    private static readonly DateTime Simdi = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DonemSonu = new(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    private static StudentVerification Kayit(string? kimlikNo = null, string ogrenciNo = "150210001") =>
        new(Guid.CreateVersion7(), "Ali Veli", "İTÜ", ogrenciNo, DonemSonu, kimlikNo);

    [Fact]
    public void ShouldBeAcceptedWithoutNationalIdentity()
    {
        var kayit = Kayit();

        Assert.Null(kayit.NationalIdentityNumber);
        Assert.Equal(StudentVerificationStatus.Pending, kayit.Status);
    }

    [Fact]
    public void IdentifierShouldFallBackToStudentNumber()
    {
        var kayit = Kayit(ogrenciNo: "150210001");

        Assert.Equal("150210001", kayit.Identifier);
        Assert.True(kayit.IdentifiedByStudentNumber);
    }

    [Fact]
    public void IdentifierShouldPreferNationalIdentityWhenPresent()
    {
        var kayit = Kayit("10000000146");

        Assert.Equal("10000000146", kayit.Identifier);
        Assert.False(kayit.IdentifiedByStudentNumber);
    }

    /// <remarks>
    /// Formdan dokunulmamis bir alan bos metin olarak gelir. Bos metin
    /// oldugu gibi saklanirsa <c>Identifier</c> "kimlik numarasi var" gibi
    /// davranip BOS deger dondururdu — ogrenciyi hicbir sey tanimlamazdi.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNationalIdentityShouldBeTreatedAsMissing(string kimlikNo)
    {
        var kayit = Kayit(kimlikNo);

        Assert.Null(kayit.NationalIdentityNumber);
        Assert.Equal("150210001", kayit.Identifier);
        Assert.True(kayit.IdentifiedByStudentNumber);
    }

    /// <remarks>
    /// Numara olmamasi eksiklik degil; ama YANLIS girilmis bir numara
    /// dogrulamayi yaniltici kilar.
    /// </remarks>
    [Theory]
    [InlineData("123")]
    [InlineData("1000000014X")]
    [InlineData("100000001466")]
    public void MalformedNationalIdentityShouldBeRejected(string kimlikNo)
    {
        Assert.Throws<DomainException>(() => Kayit(kimlikNo));
    }

    [Fact]
    public void NationalIdentityShouldBeTrimmed()
    {
        Assert.Equal("10000000146", Kayit("  10000000146  ").NationalIdentityNumber);
    }

    /// <remarks>
    /// Ogrenci numarasi TEK zorunlu kimlik alani: kimlik numarasi
    /// olmadiginda ogrenciyi ayirt eden deger bu.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyStudentNumberShouldBeRejected(string ogrenciNo)
    {
        Assert.Throws<DomainException>(() => Kayit(ogrenciNo: ogrenciNo));
    }

    [Fact]
    public void ApprovedRecordShouldBeValidUntilExpiry()
    {
        var kayit = Kayit();
        kayit.Approve(Guid.CreateVersion7(), Simdi);

        Assert.Equal(StudentVerificationStatus.Approved, kayit.Status);
        Assert.True(kayit.IsValid(Simdi));
        Assert.False(kayit.IsValid(DonemSonu));
    }

    [Fact]
    public void PendingRecordShouldNotBeValid()
    {
        Assert.False(Kayit().IsValid(Simdi));
    }

    [Fact]
    public void ExpiredDocumentShouldNotBeApprovable()
    {
        var kayit = Kayit();

        Assert.Throws<DomainException>(() =>
            kayit.Approve(Guid.CreateVersion7(), DonemSonu.AddDays(1)));
    }

    [Fact]
    public void ApprovedRecordShouldNotBeUpdatable()
    {
        var kayit = Kayit();
        kayit.Approve(Guid.CreateVersion7(), Simdi);

        Assert.Throws<DomainException>(() => kayit.Update(
            "Ali Veli", "İTÜ", "150210002", DonemSonu, null, null));
    }

    /// <remarks>
    /// Reddedilmis kayit duzeltilince yeniden incelemeye girmeli; aksi hâlde
    /// ogrenci belgeyi duzeltse bile kayit "reddedildi" olarak kalirdi.
    /// </remarks>
    [Fact]
    public void RejectedRecordShouldReturnToPendingAfterUpdate()
    {
        var kayit = Kayit();
        kayit.Reject(Guid.CreateVersion7(), Simdi, "Belge okunamiyor");

        Assert.Equal(StudentVerificationStatus.Rejected, kayit.Status);

        kayit.Update("Ali Veli", "İTÜ", "150210002", DonemSonu, null, null);

        Assert.Equal(StudentVerificationStatus.Pending, kayit.Status);
        Assert.Null(kayit.RejectionReason);
    }

    [Fact]
    public void RejectionShouldRequireReason()
    {
        var kayit = Kayit();

        Assert.Throws<DomainException>(() => kayit.Reject(Guid.CreateVersion7(), Simdi, "  "));
    }
}
