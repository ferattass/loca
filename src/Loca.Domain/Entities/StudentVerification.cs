using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Ogrenci bileti icin ogrencilik dogrulama kaydi.
/// </summary>
/// <remarks>
/// Sartname bilet turleri arasinda <c>Student</c> sayiyor ve "ogrenci bileti
/// icin dogrulama alani tasarlanmalidir" diyor. <c>TicketType</c> uzerindeki
/// <c>RequiresVerification</c> bayragi yalnizca "bu tur belge ister" der;
/// belgenin kendisi burada durur.
///
/// <para>
/// <b>Kimlik numarasi zorunlu DEGIL.</b> Yabanci uyruklu ogrencinin T.C.
/// kimlik numarasi olmaz; alani zorunlu yapmak bu ogrencilerin ogrenci
/// bileti almasini tamamen engellerdi. Kimlik numarasi yoksa
/// <see cref="Identifier"/> ogrenci numarasina duser ve kayit reddedilmez.
/// Benzersizlik de bu yuzden kimlik numarasi uzerinden degil
/// <c>UNIQUE(InstitutionName, StudentNumber)</c> uzerinden kuruluyor —
/// ogrenci numarasi her zaman var, kimlik numarasi her zaman yok.
/// </para>
/// </remarks>
public sealed class StudentVerification : BaseEntity, IOwnedResource
{
    /// <summary>T.C. kimlik numarasinin uzunlugu. Verilmisse bu kadar hane olmali.</summary>
    private const int NationalIdentityLength = 11;

    private StudentVerification()
    {
        InstitutionName = string.Empty;
        StudentNumber = string.Empty;
        FullName = string.Empty;
    }

    public StudentVerification(
        Guid userId,
        string fullName,
        string institutionName,
        string studentNumber,
        DateTime validUntilUtc,
        string? nationalIdentityNumber = null,
        Guid? documentFileId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Dogrulama bir kullaniciya bagli olmali.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Ogrencinin adi soyadi bos olamaz.");

        if (string.IsNullOrWhiteSpace(institutionName))
            throw new DomainException("Okul adi bos olamaz.");

        // Ogrenci numarasi tek zorunlu kimlik alani: kimlik numarasi
        // olmadiginda ogrenciyi ayirt eden deger bu.
        if (string.IsNullOrWhiteSpace(studentNumber))
            throw new DomainException("Ogrenci numarasi bos olamaz.");

        UserId = userId;
        FullName = fullName.Trim();
        InstitutionName = institutionName.Trim();
        StudentNumber = studentNumber.Trim();
        ValidUntilUtc = validUntilUtc;
        DocumentFileId = documentFileId;
        NationalIdentityNumber = NormalizeNationalIdentity(nationalIdentityNumber);
    }

    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public string FullName { get; private set; }

    /// <summary>Okul veya universite adi.</summary>
    public string InstitutionName { get; private set; }

    /// <summary>Okulun verdigi ogrenci numarasi. Her zaman dolu.</summary>
    public string StudentNumber { get; private set; }

    /// <summary>
    /// T.C. kimlik numarasi. <b>Opsiyonel.</b>
    /// </summary>
    /// <remarks>
    /// Bos olabilir; yabanci uyruklu ogrencide bulunmaz. Verilmisse 11 hane
    /// ve yalnizca rakam olmasi kontrol edilir — yanlis girilmis bir numara
    /// dogrulamayi yaniltici kilar, ama olmamasi bir eksiklik degildir.
    /// </remarks>
    public string? NationalIdentityNumber { get; private set; }

    /// <summary>Yuklenen ogrenci belgesi.</summary>
    public Guid? DocumentFileId { get; private set; }

    /// <summary>Belgenin gecerlilik bitisi. Genelde donem veya egitim yili sonu.</summary>
    public DateTime ValidUntilUtc { get; private set; }

    public StudentVerificationStatus Status { get; private set; } = StudentVerificationStatus.Pending;

    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    Guid IOwnedResource.OwnerId => UserId;

    /// <summary>
    /// Ogrenciyi tanimlayan deger — kullanici adi olarak bu esas alinir.
    /// </summary>
    /// <remarks>
    /// Kimlik numarasi varsa o, yoksa ogrenci numarasi. Sira bu sekilde
    /// cunku kimlik numarasi ulke capinda tekildir; ogrenci numarasi yalnizca
    /// okul icinde. Ikisi de yoksa kayit hic olusmuyor (ogrenci numarasi
    /// zorunlu), dolayisiyla bu ozellik asla bos donmez.
    /// </remarks>
    public string Identifier => NationalIdentityNumber ?? StudentNumber;

    /// <summary>Kimlik numarasi yerine ogrenci numarasi mi kullaniliyor.</summary>
    public bool IdentifiedByStudentNumber => NationalIdentityNumber is null;

    /// <summary>Belge su anda gecerli ve onayli mi.</summary>
    public bool IsValid(DateTime utcNow) =>
        Status == StudentVerificationStatus.Approved && utcNow < ValidUntilUtc;

    public void Update(
        string fullName,
        string institutionName,
        string studentNumber,
        DateTime validUntilUtc,
        string? nationalIdentityNumber,
        Guid? documentFileId)
    {
        if (Status == StudentVerificationStatus.Approved)
            throw new DomainException("Onaylanmis dogrulama kaydi degistirilemez.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Ogrencinin adi soyadi bos olamaz.");

        if (string.IsNullOrWhiteSpace(institutionName))
            throw new DomainException("Okul adi bos olamaz.");

        if (string.IsNullOrWhiteSpace(studentNumber))
            throw new DomainException("Ogrenci numarasi bos olamaz.");

        FullName = fullName.Trim();
        InstitutionName = institutionName.Trim();
        StudentNumber = studentNumber.Trim();
        ValidUntilUtc = validUntilUtc;
        NationalIdentityNumber = NormalizeNationalIdentity(nationalIdentityNumber);
        DocumentFileId = documentFileId;

        // Reddedilmis kayit duzeltilince yeniden incelemeye girer.
        Status = StudentVerificationStatus.Pending;
        RejectionReason = null;
        ReviewedAt = null;
        ReviewedBy = null;
    }

    public void Approve(Guid reviewedBy, DateTime utcNow)
    {
        if (Status == StudentVerificationStatus.Approved)
            throw new DomainException("Dogrulama zaten onaylanmis.");

        if (utcNow >= ValidUntilUtc)
            throw new DomainException("Gecerlilik suresi dolmus belge onaylanamaz.");

        Status = StudentVerificationStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = utcNow;
        RejectionReason = null;
    }

    public void Reject(Guid reviewedBy, DateTime utcNow, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Red gerekcesi zorunludur.");

        Status = StudentVerificationStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = utcNow;
        RejectionReason = reason.Trim();
    }

    /// <summary>
    /// Kimlik numarasini temizler; bos veya beyaz bosluk ise <c>null</c> doner.
    /// </summary>
    /// <remarks>
    /// Bos metin ile <c>null</c> ayrimini burada kapatmak onemli: formdan
    /// dokunulmamis bir alan bos metin olarak gelir ve veritabaninda
    /// <c>""</c> olarak saklanirsa <see cref="Identifier"/> kimlik numarasi
    /// varmis gibi davranip bos deger dondururdu.
    /// </remarks>
    private static string? NormalizeNationalIdentity(string? nationalIdentityNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalIdentityNumber))
            return null;

        var temiz = nationalIdentityNumber.Trim();

        if (temiz.Length != NationalIdentityLength || !temiz.All(char.IsAsciiDigit))
            throw new DomainException("Kimlik numarasi 11 haneli ve yalnizca rakam olmali.");

        return temiz;
    }
}
