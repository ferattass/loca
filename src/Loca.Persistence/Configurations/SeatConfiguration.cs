using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Seats");
        builder.HasKey(seat => seat.Id);

        builder.Property(seat => seat.RowLabel)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(seat => seat.IsActive)
            .HasDefaultValue(true);

        // Hesaplanan ozellik; kolon olarak yazilmaz.
        builder.Ignore(seat => seat.Label);

        // Yol haritasi Gun 4'un zorunlu kurali. Toplu uretim iki kez
        // calistirilirsa ikinci calisma bu index'e takilir — kontrol
        // uygulamada da var ama son soz veritabaninin.
        builder.HasIndex(seat => new { seat.SeatSectionId, seat.RowLabel, seat.SeatNumber })
            .IsUnique();

        // Bir plandaki aktif koltuklarin taranmasi (EventSeats uretimi, Gun 5)
        // bu index uzerinden gider.
        builder.HasIndex(seat => new { seat.SeatSectionId, seat.IsActive });

        // SeatSection ile ayni gerekce: bagli plan silindiginde koltuk da
        // sorgulardan dusmeli, yoksa zorunlu navigation null gelir.
        builder.HasQueryFilter(seat => !seat.SeatSection!.SeatLayout!.IsDeleted);
    }
}
