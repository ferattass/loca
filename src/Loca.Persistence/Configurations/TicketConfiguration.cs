using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tickets");
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.TicketNumber)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(ticket => ticket.QrCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(ticket => ticket.EventTitle)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ticket => ticket.SeatLabel)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(ticket => ticket.TicketTypeName)
            .HasMaxLength(100)
            .IsRequired();

        // Kesildigi andaki tutar kopyalanir; TicketType/EventSeat fiyati
        // sonradan degisse de kesilmis bilet bu tutari korur.
        builder.ComplexProperty(
            ticket => ticket.Price,
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

        // Kullaniciya gosterilen numara. Tekil: iki biletin ayni numarayi
        // tasimasi bilet dogrulama ekraninda hangi satisin gecerli oldugunu
        // belirsizlestirirdi.
        builder.HasIndex(ticket => ticket.TicketNumber)
            .IsUnique()
            .HasDatabaseName("IX_Tickets_TicketNumber");

        // Giriste okutulan kod. Tekil VE tahmin edilemez olmali; tekillik
        // burada, tahmin edilemezlik uretici tarafinda (TicketCodeGenerator).
        builder.HasIndex(ticket => ticket.QrCode)
            .IsUnique()
            .HasDatabaseName("IX_Tickets_QrCode");

        // SON SAVUNMA HATTI: bir rezervasyon kalemi yalnizca bir bilet
        // uretebilir. Ayni odeme bildirimi uygulama katmanindaki kontrolu
        // (ExistsForReservationAsync) bir sekilde atlatip iki kez islense
        // bile, bu tekil kisit ikinci INSERT'i veritabani seviyesinde
        // reddeder.
        builder.HasIndex(ticket => ticket.ReservationItemId)
            .IsUnique()
            .HasDatabaseName("IX_Tickets_ReservationItemId");

        builder.HasIndex(ticket => ticket.UserId);
        builder.HasIndex(ticket => ticket.ReservationId);
        builder.HasIndex(ticket => ticket.EventSessionId);

        builder.HasOne(ticket => ticket.Reservation)
            .WithMany()
            .HasForeignKey(ticket => ticket.ReservationId)
            // Satilmis bilet mali/hukuki bir belge; rezervasyon silinse de
            // bilet kaydi kalmali.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ticket => ticket.User)
            .WithMany()
            .HasForeignKey(ticket => ticket.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Asagidaki dort iliski icin Ticket entity'sinde CLR navigasyonu
        // yok (yalnizca Guid alan) — bilet kesildigi andaki bilgiyi zaten
        // KOPYALADIGI icin bu iliskilere okuma tarafinda ihtiyac duymuyor.
        // Yine de FK veritabaninda kuruluyor: referans butunlugu, "var olmayan
        // bir etkinlige ait bilet" gibi imkansiz bir durumu daha en basta
        // engelliyor.
        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(ticket => ticket.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EventSession>()
            .WithMany()
            .HasForeignKey(ticket => ticket.EventSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EventSeat>()
            .WithMany()
            .HasForeignKey(ticket => ticket.EventSeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TicketType>()
            .WithMany()
            .HasForeignKey(ticket => ticket.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ReservationItem>()
            .WithMany()
            .HasForeignKey(ticket => ticket.ReservationItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ticket'in ZORUNLU ve filtreli tek CLR navigasyonu Reservation;
        // Reservation'in kendi filtresi EventSession uzerinden Event/Hall/
        // SeatLayout'a bakiyor, o kosul burada aynen tekrarlaniyor — aksi
        // hâlde silinmis bir mekanin biletleri sorgudan donup Reservation
        // navigasyonu null gelebilir (Gun 4/5 tuzagi).
        //
        // EventSeat de ayni sekilde filtreli bir varlik ama Ticket'ten ona
        // giden bir CLR navigasyonu yok (yalnizca EventSeatId), bu yuzden
        // query filter ifadesinde adi gecemiyor. Pratikte sorun degil:
        // Ticket'in ReservationId'si ile EventSeatId'si ayni EventSession'a
        // baglanacak sekilde uretiliyor (bkz. CompletePaymentCommand), yani
        // Reservation zinciri zaten ayni Event/Hall/SeatLayout'u kontrol
        // etmis oluyor.
        builder.HasQueryFilter(ticket =>
            !ticket.Reservation!.EventSession!.Event!.IsDeleted &&
            !ticket.Reservation!.EventSession!.Hall!.IsDeleted &&
            !ticket.Reservation!.EventSession!.SeatLayout!.IsDeleted);
    }
}
