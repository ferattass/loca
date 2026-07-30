using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(log => log.Action)
            .IsRequired()
            .HasMaxLength(50);

        // jsonb, text degil: PostgreSQL jsonb'yi ayristirilmis olarak saklar,
        // boylece "eski fiyati 500 olan kayitlar" gibi sorgular alan bazinda
        // yazilabilir. text olsaydi her sorgu metin arama olurdu.
        builder.Property(log => log.OldValues)
            .HasColumnType("jsonb");

        builder.Property(log => log.NewValues)
            .HasColumnType("jsonb");

        builder.Property(log => log.CorrelationId)
            .HasMaxLength(64);

        builder.Property(log => log.IpAddress)
            .HasMaxLength(45);

        // Denetim ekraninin ana sorgusu: "su varligin su kaydinda ne oldu".
        builder.HasIndex(log => new { log.EntityName, log.EntityId });

        // Zaman siralamasi: en yeni kayitlar ustte.
        builder.HasIndex(log => log.OccurredAtUtc);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            // Kullanici silinse bile denetim kaydi kalmali: denetimin amaci
            // gecmisi korumak. Kayit silinirse denetim delinir.
            .OnDelete(DeleteBehavior.SetNull);
    }
}
