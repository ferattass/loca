using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class OrganizerProfileConfiguration : IEntityTypeConfiguration<OrganizerProfile>
{
    public void Configure(EntityTypeBuilder<OrganizerProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrganizerProfiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(profile => profile.TaxNumber)
            .HasMaxLength(20);

        builder.Property(profile => profile.ContactEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(profile => profile.ContactPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(profile => profile.Website)
            .HasMaxLength(300);

        builder.Property(profile => profile.IsVerified)
            .HasDefaultValue(false);

        // Bire bir: bir kullanicinin en fazla bir organizator profili olur.
        builder.HasIndex(profile => profile.UserId).IsUnique();

        builder.HasOne(profile => profile.User)
            .WithMany()
            .HasForeignKey(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrganizerApplicationConfiguration : IEntityTypeConfiguration<OrganizerApplication>
{
    public void Configure(EntityTypeBuilder<OrganizerApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrganizerApplications");
        builder.HasKey(application => application.Id);

        builder.Property(application => application.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(application => application.TaxNumber)
            .HasMaxLength(20);

        builder.Property(application => application.ContactEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(application => application.ContactPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(application => application.Website)
            .HasMaxLength(300);

        builder.Property(application => application.RejectionReason)
            .HasMaxLength(500);

        // Basvuru gecmisi tutuldugu icin UserId tekil DEGIL: reddedilen
        // kullanici duzeltip yeniden basvurabilir. "Ayni anda birden fazla
        // bekleyen basvuru olmamali" kurali handler'da kontrol ediliyor —
        // kismi unique index ile de yazilabilirdi ama o zaman kurali
        // degistirmek migration gerektirirdi.
        builder.HasIndex(application => application.UserId);

        // Admin kuyrugu: bekleyen basvurular.
        builder.HasIndex(application => application.Status);

        builder.HasOne(application => application.User)
            .WithMany()
            .HasForeignKey(application => application.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(application => application.ReviewedBy)
            // Basvuruyu inceleyen admin silinirse basvuru kaydi kalir,
            // yalnizca inceleyen bilgisi bosalir.
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<UploadedFile>()
            .WithMany()
            .HasForeignKey(application => application.DocumentFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
