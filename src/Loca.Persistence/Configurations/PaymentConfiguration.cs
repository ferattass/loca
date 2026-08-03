using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Payments");
        builder.HasKey(payment => payment.Id);

        // Money deger nesnesi iki koluna aciliyor. decimal(18,2) — float
        // ASLA: tahsilat tutari ikilik kayan noktada tam temsil edilemez ve
        // hata satis sayisiyla birikir.
        builder.ComplexProperty(
            payment => payment.Amount,
            money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("Amount_Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Amount_Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

        builder.Property(payment => payment.Provider)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(payment => payment.ProviderReference)
            .HasMaxLength(200);

        builder.Property(payment => payment.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(payment => payment.FailureReason)
            .HasMaxLength(500);

        // Rezervasyon akisindaki idempotency kisitiyla ayni gerekce: ayni
        // istegin iki kez islenmesi uygulama kontrolune degil veritabanina
        // birakilirsa yarisi kaybedilebilir.
        builder.HasIndex(payment => new { payment.UserId, payment.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_Payments_UserId_IdempotencyKey");

        // REZERVASYON BASINA EN FAZLA BIR BASARILI ODEME.
        // Index tum satirlari kapsasaydi ayni rezervasyonun basarisiz
        // denemeleri (Failed, Cancelled) ikinci denemeyi engellerdi; oysa
        // saglayici reddettikten sonra kullanicinin yeniden denemesi
        // gerekiyor. Kismi index yalnizca BASARILI (Status = Succeeded)
        // satiri kapsiyor: ayni rezervasyona iki basarili odeme veritabani
        // seviyesinde imkansiz, basarisiz denemeler serbest kaliyor.
        builder.HasIndex(payment => payment.ReservationId)
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)PaymentStatus.Succeeded}")
            .HasDatabaseName("IX_Payments_ReservationId_Succeeded");

        // Saglayici callback'i geldiginde "bu islem kimligi hangi odemeye
        // ait" sorgusu buradan gecer.
        builder.HasIndex(payment => payment.ProviderReference);

        builder.HasOne(payment => payment.Reservation)
            .WithMany()
            .HasForeignKey(payment => payment.ReservationId)
            // Odeme mali kayittir; rezervasyon (soft delete olmasa da)
            // silinse dahi odeme kaydi kalmali.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.User)
            .WithMany()
            .HasForeignKey(payment => payment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Islemler disariya IReadOnlyList olarak aciliyor; EF alani dogrudan
        // doldurmali, aksi hâlde salt-okunur listeye yazmaya calisirdi.
        builder.Metadata
            .FindNavigation(nameof(Payment.Transactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(payment => payment.Transactions)
            .WithOne(transaction => transaction.Payment)
            .HasForeignKey(transaction => transaction.PaymentId)
            // Islem izi odemeden bagimsiz anlamsiz; odeme silinirse gider.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(payment => payment.IsPending);
        builder.Ignore(payment => payment.IsSuccessful);

        // Reservation ZORUNLU bagli ve kendi filtresi var (EventSession
        // uzerinden Event/Hall/SeatLayout soft delete kontrolu). Eslesen
        // kosul yazilmazsa silinmis bir mekanin rezervasyonuna ait odeme
        // sorgudan donup Reservation navigasyonu null gelebilir — Gun 4 ve
        // Gun 5'teki tuzagin ayni tekrari.
        builder.HasQueryFilter(payment =>
            !payment.Reservation!.EventSession!.Event!.IsDeleted &&
            !payment.Reservation!.EventSession!.Hall!.IsDeleted &&
            !payment.Reservation!.EventSession!.SeatLayout!.IsDeleted);
    }
}

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PaymentTransactions");
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.ProviderReference)
            .HasMaxLength(200);

        builder.Property(transaction => transaction.Message)
            .HasMaxLength(500);

        // Odeme detayinin islem gecmisi sorgusu: "bu odemenin denemeleri".
        builder.HasIndex(transaction => transaction.PaymentId);

        // Bu tablo normalde Payment.Transactions uzerinden okunuyor ama
        // dogrudan DbSet olarak da erisilebilir (Reservation/ReservationItem
        // ikilisindeki gibi). Payment zaten filtreli oldugu icin ayni kosul
        // Payment uzerinden burada da tekrarlaniyor; aksi hâlde dogrudan
        // sorgu silinmis bir mekana ait odemenin islem satirlarini dondurebilirdi.
        builder.HasQueryFilter(transaction =>
            !transaction.Payment!.Reservation!.EventSession!.Event!.IsDeleted &&
            !transaction.Payment!.Reservation!.EventSession!.Hall!.IsDeleted &&
            !transaction.Payment!.Reservation!.EventSession!.SeatLayout!.IsDeleted);
    }
}
