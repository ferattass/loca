using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class StudentVerificationConfiguration : IEntityTypeConfiguration<StudentVerification>
{
    public void Configure(EntityTypeBuilder<StudentVerification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StudentVerifications");
        builder.HasKey(verification => verification.Id);

        builder.Property(verification => verification.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(verification => verification.InstitutionName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(verification => verification.StudentNumber)
            .IsRequired()
            .HasMaxLength(50);

        // ZORUNLU DEGIL. Yabanci uyruklu ogrencinin kimlik numarasi olmaz;
        // IsRequired() yazilsa bu ogrenciler ogrenci bileti alamazdi.
        builder.Property(verification => verification.NationalIdentityNumber)
            .HasMaxLength(11);

        builder.Property(verification => verification.RejectionReason)
            .HasMaxLength(500);

        // Hesaplanan ozellikler: kimlik numarasi varsa o, yoksa ogrenci
        // numarasi. Kolon olarak yazilsaydi iki kaynaktan turetilen bir deger
        // ucuncu kez saklanmis olur ve senkron sorunu dogardi.
        builder.Ignore(verification => verification.Identifier);
        builder.Ignore(verification => verification.IdentifiedByStudentNumber);

        // BENZERSIZLIK OGRENCI NUMARASI UZERINDEN KURULUYOR.
        // Kimlik numarasi her zaman yok, ogrenci numarasi her zaman var;
        // tekillik kuralinin dayanagi bu yuzden (okul, ogrenci no) ikilisi.
        // Ogrenci numarasi yalnizca okul icinde tekil oldugu icin okul adi
        // da anahtara giriyor.
        builder.HasIndex(verification => new
        {
            verification.InstitutionName,
            verification.StudentNumber
        }).IsUnique();

        // Kimlik numarasi verilmisse ulke capinda tekil olmali. Kismi index:
        // NULL satirlar kisita girmiyor, aksi hâlde kimlik numarasi olmayan
        // ikinci ogrenci "ayni NULL" yuzunden reddedilirdi.
        builder.HasIndex(verification => verification.NationalIdentityNumber)
            .IsUnique()
            .HasFilter("\"NationalIdentityNumber\" IS NOT NULL");

        builder.HasIndex(verification => verification.UserId);

        builder.HasOne(verification => verification.User)
            .WithMany()
            .HasForeignKey(verification => verification.UserId)
            // Kullanici silinirse dogrulama kaydi da gitmeli: kisisel veri,
            // sahibi olmadan tutulmasinin gerekcesi yok.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UploadedFile>()
            .WithMany()
            .HasForeignKey(verification => verification.DocumentFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
