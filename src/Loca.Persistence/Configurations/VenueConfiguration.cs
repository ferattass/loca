using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Venues");
        builder.HasKey(venue => venue.Id);

        builder.Property(venue => venue.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(venue => venue.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(venue => venue.Description)
            .HasMaxLength(2000);

        builder.Property(venue => venue.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(venue => venue.IsActive)
            .HasDefaultValue(true);

        builder.Property(venue => venue.IsDeleted)
            .HasDefaultValue(false);

        // Sehre gore mekan listeleme en sik sorgu; silinmisler zaten
        // global filtreyle disarida kaliyor.
        builder.HasIndex(venue => venue.CityId);

        builder.HasOne(venue => venue.City)
            .WithMany()
            .HasForeignKey(venue => venue.CityId)
            // Sehir silinirse mekanlar da gitmemeli: sehir kaydi referans
            // verisidir, silinmesi bir veri temizligi hatasi olur.
            .OnDelete(DeleteBehavior.Restrict);

        // Kapak gorseli silinirse mekan kaydi ayakta kalir, yalnizca gorsel
        // baglantisi bosalir. Navigation ozelligi yok; entity yalnizca
        // kimligi tasiyor, dosyanin kendisi UploadedFiles'ta.
        builder.HasOne<UploadedFile>()
            .WithMany()
            .HasForeignKey(venue => venue.ImageFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(venue => venue.Halls)
            .WithOne(hall => hall.Venue)
            .HasForeignKey(hall => hall.VenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(Venue.Halls))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
