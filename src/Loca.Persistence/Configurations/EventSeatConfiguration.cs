using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

/// <summary>
/// Projenin kalbi. Gun 6'daki eszamanlilik cozumu bu tablonun
/// kisitlarina dayaniyor.
/// </summary>
internal sealed class EventSeatConfiguration : IEntityTypeConfiguration<EventSeat>
{
    public void Configure(EntityTypeBuilder<EventSeat> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventSeats");
        builder.HasKey(seat => seat.Id);

        builder.ComplexProperty(
            seat => seat.Price,
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

        // PostgreSQL'in xmin sistem kolonu satirin surumunu tutar; her
        // UPDATE'te kendiliginden degisir. Ayri bir RowVersion kolonu
        // tutulup elle artirilsaydi artirmayi unutan tek bir kod yolu
        // eszamanlilik korumasini sessizce devre disi birakirdi.
        // xmin bir sistem kolonu oldugu icin tabloya yeni kolon eklenmiyor.
        builder.Property(seat => seat.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // YARIS DURUMUNUN SON SAVUNMA HATTI.
        // Redis kilidi ve concurrency token asilsa bile ayni koltugun ayni
        // oturumda iki satiri olusamaz. Kabul olcutu: 50 paralel istekte
        // tam olarak 1 basari.
        builder.HasIndex(seat => new { seat.EventSessionId, seat.SeatId })
            .IsUnique();

        // Bos koltuk sorgusu: "bu oturumda durumu Available olanlar".
        // seat-availability ucunun ana sorgusu.
        builder.HasIndex(seat => new { seat.EventSessionId, seat.Status });

        // Suresi dolan kilitleri toplayan arka plan isi (Gun 7) yalnizca
        // kilitli satirlarla ilgileniyor; kismi index tablonun tamamini
        // degil yuzde birini tasiyor.
        builder.HasIndex(seat => seat.LockedUntilUtc)
            .HasFilter($"\"Status\" = {(int)EventSeatStatus.Locked}");

        // Rezervasyon detayi: "bu rezervasyonun koltuklari".
        builder.HasIndex(seat => seat.ReservationId);

        builder.HasOne(seat => seat.EventSession)
            .WithMany()
            .HasForeignKey(seat => seat.EventSessionId)
            // Oturum silinirse satilabilir koltuklari da gitmeli.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(seat => seat.Seat)
            .WithMany()
            .HasForeignKey(seat => seat.SeatId)
            // Fiziksel koltuk silinemez: satilmis bilet ona referans veriyor.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(seat => seat.TicketType)
            .WithMany()
            .HasForeignKey(seat => seat.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(seat => seat.LockedByUserId)
            // Kullanici silinirse koltuk kilidi sahipsiz kalir, koltuk
            // silinmez. Kilit zaten TTL ile dusuyor.
            .OnDelete(DeleteBehavior.SetNull);

        // Zorunlu bagli oldugu her filtreli varlik icin eslesen kosul.
        // Zincir uzun gorunuyor ama joinlerin tamami birincil anahtar
        // uzerinde; maliyeti Gun 5'in N+1 olcumunde kontrol edildi.
        // Kosullar yazilmazsa silinmis bir planin koltugu sorgudan donebilir
        // ve zorunlu navigation null gelir.
        builder.HasQueryFilter(seat =>
            !seat.EventSession!.Event!.IsDeleted &&
            !seat.EventSession!.Hall!.IsDeleted &&
            !seat.EventSession!.SeatLayout!.IsDeleted &&
            !seat.Seat!.SeatSection!.SeatLayout!.IsDeleted);
    }
}
