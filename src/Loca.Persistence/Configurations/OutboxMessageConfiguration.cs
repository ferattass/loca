using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type)
            .HasMaxLength(100)
            .IsRequired();

        // jsonb: metin kolonu olsaydi govde icinde arama tam tablo taramasi
        // gerektirirdi; PostgreSQL jsonb'yi ayristirip index'leyebiliyor.
        builder.Property(message => message.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.ErrorMessage)
            .HasMaxLength(1000);

        // ISLENMEYI BEKLEYEN KUYRUGUN ANA SORGUSU (GetPendingAsync).
        // Kismi index yalnizca ProcessedAtUtc IS NULL olan satirlari
        // tasiyor. Islenmis mesajlar zamanla tablonun buyuk cogunlugu
        // olacak; tam index olsaydi arka plan isi kuyruk buyudukce
        // yavaslardi.
        builder.HasIndex(message => message.OccurredAtUtc)
            .HasFilter("\"ProcessedAtUtc\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_OccurredAtUtc_Pending");

        builder.Ignore(message => message.IsProcessed);
        builder.Ignore(message => message.CanRetry);
        builder.Ignore(message => message.IsDeadLettered);
    }
}
