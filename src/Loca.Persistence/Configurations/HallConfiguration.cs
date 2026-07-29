using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Halls");
        builder.HasKey(hall => hall.Id);

        builder.Property(hall => hall.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(hall => hall.IsActive)
            .HasDefaultValue(true);

        builder.Property(hall => hall.IsDeleted)
            .HasDefaultValue(false);

        // Ayni mekanda ayni adla iki salon olmasin: "Buyuk Salon" iki kez
        // girildiginde organizator hangisini sectigini bilemez.
        builder.HasIndex(hall => new { hall.VenueId, hall.Name }).IsUnique();

        builder.HasMany(hall => hall.SeatLayouts)
            .WithOne(layout => layout.Hall)
            .HasForeignKey(layout => layout.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(Hall.SeatLayouts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
