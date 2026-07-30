using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketTypes");
        builder.HasKey(ticketType => ticketType.Id);

        builder.Property(ticketType => ticketType.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ticketType => ticketType.RequiresVerification)
            .HasDefaultValue(false);

        builder.Property(ticketType => ticketType.IsActive)
            .HasDefaultValue(true);

        // Money deger nesnesi iki kolona aciliyor: Price_Amount ve
        // Price_Currency. decimal(18,2) — float ASLA, ikilik kayan nokta
        // ondaligi tam temsil edemez ve tutar hatasi satis sayisiyla birikir.
        builder.ComplexProperty(
            ticketType => ticketType.Price,
            price =>
            {
                price.Property(money => money.Amount)
                    .HasColumnName("Price_Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                price.Property(money => money.Currency)
                    .HasColumnName("Price_Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

        // Sartname: aynı etkinlikte aynı isimde iki bilet turu olmamali.
        // Domain tarafinda da kontrol var; son soz veritabaninin.
        builder.HasIndex(ticketType => new { ticketType.EventId, ticketType.Name })
            .IsUnique();

        // "Bu bolume atanmis aktif tur var mi" sorgusu ve koltuk uretiminde
        // bolum -> tur eslemesi bu index uzerinden gider.
        builder.HasIndex(ticketType => new { ticketType.EventId, ticketType.SeatSectionId });

        builder.HasOne<SeatSection>()
            .WithMany()
            .HasForeignKey(ticketType => ticketType.SeatSectionId)
            // Bolum silinirse bilet turu varsayilan tur hâline dusmeli,
            // silinmemeli: satilmis biletler bu ture referans veriyor.
            .OnDelete(DeleteBehavior.SetNull);

        // Etkinlik soft delete, bilet turu ona zorunlu bagli — oturumdaki
        // ile ayni gerekce.
        builder.HasQueryFilter(ticketType => !ticketType.Event!.IsDeleted);
    }
}
