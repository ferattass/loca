using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class SeatSectionConfiguration : IEntityTypeConfiguration<SeatSection>
{
    public void Configure(EntityTypeBuilder<SeatSection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SeatSections");
        builder.HasKey(section => section.Id);

        builder.Property(section => section.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Ayni planda ayni adla iki bolum karisiklik yaratir: bilet turu
        // atamasi bolum adiyla yapiliyor.
        builder.HasIndex(section => new { section.SeatLayoutId, section.Name }).IsUnique();

        builder.HasMany(section => section.Seats)
            .WithOne(seat => seat.SeatSection)
            .HasForeignKey(seat => seat.SeatSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(SeatSection.Seats))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Bolum kendisi silinebilir degil ama bagli oldugu plan silinebilir.
        // Esleyen bir filtre yazilmazsa EF uyariyor ve hakli: plan
        // filtrelenip disarida kalirken bolum sorgudan donerse
        // section.SeatLayout null gelir — oysa iliski zorunlu.
        builder.HasQueryFilter(section => !section.SeatLayout!.IsDeleted);
    }
}
