using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Reservations");
        builder.HasKey(reservation => reservation.Id);

        builder.ComplexProperty(
            reservation => reservation.TotalAmount,
            price =>
            {
                price.Property(money => money.Amount)
                    .HasColumnName("Total_Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                price.Property(money => money.Currency)
                    .HasColumnName("Total_Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

        // Hizmet bedeli AYRI kolonda saklaniyor, toplamdan turetilmiyor:
        // yuzde sonradan degistiginde eski rezervasyonlarin o gunku bedeli
        // oldugu gibi kalmali. Gecmise donuk bir rapor bugunku yuzdeyle
        // hesaplasaydi yanlis rakamlar cikardi.
        builder.ComplexProperty(
            reservation => reservation.ServiceFee,
            fee =>
            {
                fee.Property(money => money.Amount)
                    .HasColumnName("ServiceFee_Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                fee.Property(money => money.Currency)
                    .HasColumnName("ServiceFee_Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

        // Kalemler toplami HESAPLANIYOR (Total - ServiceFee), saklanmiyor:
        // uc tutar da saklansaydi birbirini tutmama ihtimali dogardi.
        builder.Ignore(reservation => reservation.ItemsTotal);

        builder.Property(reservation => reservation.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        // IDEMPOTENCY'NIN GERCEK GARANTISI BURADA.
        // Handler'daki "bu anahtarla kayit var mi" sorgusu yarisi kaybedebilir:
        // iki istek ayni anda bakip ikisi de bos bulabilir. Kisit veritabani
        // seviyesinde oldugu icin ikisinden yalnizca biri yazabilir; kaybeden
        // istek hatayi yakalayip kazananin kaydini doner.
        builder.HasIndex(reservation => new { reservation.UserId, reservation.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName(DatabaseConstraints.ReservationIdempotencyKey);

        // Suresi dolanlari toplayan arka plan isinin sorgusu:
        // "Status = Pending ve ExpiresAtUtc gecmis". Kismi index yalnizca
        // bekleyen kayitlari tasiyor; tamamlanmis rezervasyonlar zamanla
        // tablonun cogunlugu olacak ve bu isi hic ilgilendirmiyorlar.
        builder.HasIndex(reservation => reservation.ExpiresAtUtc)
            .HasFilter($"\"Status\" = {(int)ReservationStatus.Pending}")
            .HasDatabaseName("IX_Reservations_ExpiresAtUtc_Pending");

        // "Kullanicinin bu oturumdaki aktif bilet sayisi" sorgusu — her
        // rezervasyon isteginde calisiyor.
        builder.HasIndex(reservation => new { reservation.UserId, reservation.EventSessionId });

        builder.HasOne(reservation => reservation.User)
            .WithMany()
            .HasForeignKey(reservation => reservation.UserId)
            // Rezervasyon gecmisi kullanici silinse de kalir: satis kaydi
            // muhasebe belgesi. Kullanici silme zaten soft delete.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.EventSession)
            .WithMany()
            .HasForeignKey(reservation => reservation.EventSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Kalemler yalnizca aggregate uzerinden okunur/yazilir; EF alanı
        // dogrudan kullanir, ozelligin uzerinden gitmez. Aksi hâlde
        // IReadOnlyList olan Items uzerine yazmaya calisirdi.
        builder.Metadata
            .FindNavigation(nameof(Reservation.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(reservation => reservation.Items)
            .WithOne(item => item.Reservation)
            .HasForeignKey(item => item.ReservationId)
            // Rezervasyon silinirse kalemleri de gitmeli; kalem tek basina
            // anlamsiz.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(reservation => reservation.SeatCount);

        // Oturum filtreli oldugu icin ona ZORUNLU bagli olan bu varlikta da
        // eslesen kosul olmali — Gun 4 ve Gun 5'te ayni tuzaga iki kez
        // dusuldu: filtresi olmayan taraf, filtrelenmis bir kaydin cocugunu
        // dondurup zorunlu navigation'i null birakiyor.
        builder.HasQueryFilter(reservation =>
            !reservation.EventSession!.Event!.IsDeleted &&
            !reservation.EventSession!.Hall!.IsDeleted &&
            !reservation.EventSession!.SeatLayout!.IsDeleted);
    }
}

internal sealed class ReservationItemConfiguration : IEntityTypeConfiguration<ReservationItem>
{
    public void Configure(EntityTypeBuilder<ReservationItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReservationItems");
        builder.HasKey(item => item.Id);

        builder.ComplexProperty(
            item => item.Price,
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

        // Index var ama TEKIL DEGIL.
        // Ilk yazilista tekil yapilmisti; yanlisti. Iptal edilen veya suresi
        // dolan bir rezervasyonun kalemi silinmiyor (gecmis kaydi), koltuk ise
        // serbest kalip yeniden satiliyor. Tekil olsaydi ayni koltugun ikinci
        // kez rezerve edilmesi veritabani hatasina duserdi — yani normal
        // isleyis, "guvenlik" diye konmus bir kisit yuzunden kirilirdi.
        // Ayni koltugun AYNI ANDA iki aktif rezervasyona baglanmasini
        // engelleyen sey bu index degil, EventSeat.Status ile xmin damgasi.
        builder.HasIndex(item => item.EventSeatId);

        builder.HasIndex(item => item.ReservationId);

        builder.HasOne(item => item.EventSeat)
            .WithMany()
            .HasForeignKey(item => item.EventSeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.TicketType)
            .WithMany()
            .HasForeignKey(item => item.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zorunlu bagli oldugu EventSeat filtreli; kosul eslestirilmezse
        // silinmis bir planin koltugunun kalemi sorgudan donebilir.
        builder.HasQueryFilter(item =>
            !item.EventSeat!.EventSession!.Event!.IsDeleted &&
            !item.EventSeat!.EventSession!.Hall!.IsDeleted &&
            !item.EventSeat!.EventSession!.SeatLayout!.IsDeleted &&
            !item.EventSeat!.Seat!.SeatSection!.SeatLayout!.IsDeleted);
    }
}
