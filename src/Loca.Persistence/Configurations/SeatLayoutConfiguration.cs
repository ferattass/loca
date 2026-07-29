using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class SeatLayoutConfiguration : IEntityTypeConfiguration<SeatLayout>
{
    public void Configure(EntityTypeBuilder<SeatLayout> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SeatLayouts");
        builder.HasKey(layout => layout.Id);

        builder.Property(layout => layout.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(layout => layout.Description)
            .HasMaxLength(1000);

        builder.Property(layout => layout.IsActive)
            .HasDefaultValue(true);

        builder.Property(layout => layout.IsDeleted)
            .HasDefaultValue(false);

        // Yol haritasi Gun 4: ayni salonda ayni isimde iki plan olamaz.
        builder.HasIndex(layout => new { layout.HallId, layout.Name }).IsUnique();

        builder.HasMany(layout => layout.Sections)
            .WithOne(section => section.SeatLayout)
            .HasForeignKey(section => section.SeatLayoutId)
            // Plan soft delete edilir, fiziksel silinmez. Yine de bir plan
            // hic kullanilmadan silinirse bolumleri de gitmeli — bolum tek
            // basina anlamli degil, planin parcasi.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(SeatLayout.Sections))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
