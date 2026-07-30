using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Events");
        builder.HasKey(ev => ev.Id);

        builder.Property(ev => ev.Title)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(ev => ev.Description)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(ev => ev.CancellationPolicy)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(ev => ev.CancellationReason)
            .HasMaxLength(500);

        builder.Property(ev => ev.IsDeleted)
            .HasDefaultValue(false);

        // Deger nesnesi olarak okunan hesaplanmis ozellikler; kolon degiller.
        // Tarih ve yer alanlari duz kolon olarak zaten yaziliyor.
        builder.Ignore(ev => ev.Place);
        builder.Ignore(ev => ev.Schedule);
        builder.Ignore(ev => ev.IsSalesActive);
        builder.Ignore(ev => ev.AllowsCriticalChanges);
        builder.Ignore(ev => ev.DefaultTicketType);

        // Ana listeleme sorgusu: sehir + kategori + tarih. Uc kolon birlikte
        // filtrelendigi icin tek tek index yerine bilesik index gerekiyor;
        // ayri index'ler olsaydi PostgreSQL yalnizca birini kullanabilirdi.
        builder.HasIndex(ev => new { ev.CityId, ev.CategoryId, ev.EventDateUtc });

        // "Yaklasan etkinlikler" ve tarih araligi filtresi icin.
        builder.HasIndex(ev => ev.EventDateUtc);

        // Organizatorun kendi etkinlikleri — panelin ilk sorgusu.
        builder.HasIndex(ev => ev.OrganizerId);

        // Durum bazli listeleme (yayindakiler, onay bekleyenler).
        builder.HasIndex(ev => ev.Status);

        builder.HasOne(ev => ev.Category)
            .WithMany()
            .HasForeignKey(ev => ev.CategoryId)
            // Kategori referans verisi; silinmesi etkinligi de silmemeli.
            .OnDelete(DeleteBehavior.Restrict);

        // Organizator, sehir, mekan ve salon navigation'lari OKUMA tarafi
        // icin duruyor: detay sorgusu projeksiyonda ad alanlarini tek
        // SELECT ile aliyor. Yazma tarafinda kullanilmiyorlar, bu yuzden
        // Include ile yuklenmiyor ve aggregate agirlasmiyor.
        builder.HasOne(ev => ev.Organizer)
            .WithMany()
            .HasForeignKey(ev => ev.OrganizerId)
            // Etkinligi olan kullanici silinemez: gecmis satislar bu
            // etkinlige, etkinlik de bu kullaniciya bagli.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ev => ev.City)
            .WithMany()
            .HasForeignKey(ev => ev.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ev => ev.Venue)
            .WithMany()
            .HasForeignKey(ev => ev.VenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ev => ev.Hall)
            .WithMany()
            .HasForeignKey(ev => ev.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // Afis silinirse etkinlik ayakta kalir, yalnizca gorsel baglantisi
        // bosalir — mekan kapak gorseliyle ayni gerekce.
        builder.HasOne<UploadedFile>()
            .WithMany()
            .HasForeignKey(ev => ev.PosterFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(ev => ev.Sessions)
            .WithOne(session => session.Event)
            .HasForeignKey(session => session.EventId)
            // Etkinlik silinirse oturumlari da gitmeli: oturum etkinlik
            // olmadan anlamsiz. Etkinlik zaten soft delete oldugu icin bu
            // cascade pratikte yalnizca gercek silmede devreye girer.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(ev => ev.TicketTypes)
            .WithOne(ticketType => ticketType.Event)
            .HasForeignKey(ticketType => ticketType.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Koleksiyonlar disariya IReadOnlyCollection olarak aciliyor;
        // EF'in alani dogrudan doldurmasi gerekiyor.
        builder.Metadata
            .FindNavigation(nameof(Event.Sessions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(Event.TicketTypes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Mekan ve salon soft delete; etkinlik ikisine de ZORUNLU bagli.
        // Eslesen filtre yazilmazsa silinmis mekanin etkinligi sorgudan
        // donebilir ve ev.Venue null gelir — Gun 4'te SeatSection ile ayni
        // tuzak. Etkinligin kendi !IsDeleted filtresi buna EKLENIR
        // (LocaDbContext filtreleri birlestiriyor), ezmez.
        builder.HasQueryFilter(ev => !ev.Venue!.IsDeleted && !ev.Hall!.IsDeleted);
    }
}
