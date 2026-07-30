using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class EventSessionConfiguration : IEntityTypeConfiguration<EventSession>
{
    public void Configure(EntityTypeBuilder<EventSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventSessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.SeatsGenerated)
            .HasDefaultValue(false);

        // Hesaplanan ozellik: bitis + temizlik payi. Kolon olarak yazilsaydi
        // temizlik payi degistiginde tum satirlarin guncellenmesi gerekirdi.
        builder.Ignore(session => session.OccupiedUntilUtc);

        // Salon cakisma sorgusunun index'i. Bu sorgu her oturum eklemede
        // calisiyor ve "bu salonda su aralikta oturum var mi" diye soruyor;
        // index olmadan tum oturum tablosu taranir.
        builder.HasIndex(session => new { session.HallId, session.StartsAtUtc, session.EndsAtUtc });

        builder.HasIndex(session => session.EventId);

        builder.HasOne(session => session.Hall)
            .WithMany()
            .HasForeignKey(session => session.HallId)
            // Aktif oturumu olan salon silinemez (Gun 4 kurali); Restrict
            // bu kurali veritabani seviyesinde de bagliyor.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(session => session.SeatLayout)
            .WithMany()
            .HasForeignKey(session => session.SeatLayoutId)
            // Kullanilmis oturma plani silinemez: uretilmis EventSeats
            // satirlari bu planin koltuklarina referans veriyor.
            .OnDelete(DeleteBehavior.Restrict);

        // Oturum kendisi soft delete DEGIL ama uc zorunlu bagi da filtreli
        // varliklara: etkinlik, salon ve oturma plani. Ucu icin de eslesen
        // kosul yazilmazsa silinmis bir kaydin oturumu sorgudan donebilir ve
        // zorunlu navigation null gelir — Gun 4'te SeatSection ile ayni tuzak.
        builder.HasQueryFilter(session =>
            !session.Event!.IsDeleted &&
            !session.Hall!.IsDeleted &&
            !session.SeatLayout!.IsDeleted);
    }
}
