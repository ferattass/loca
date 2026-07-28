using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.Token)
            .IsRequired()
            .HasMaxLength(200);

        // Veri modeli §5: token ile arama yenileme akisinin ilk adimi.
        builder.HasIndex(token => token.Token).IsUnique();
        builder.HasIndex(token => token.UserId);

        // IPv6 adresi 45 karaktere kadar cikabilir.
        builder.Property(token => token.CreatedByIp).HasMaxLength(45);
        builder.Property(token => token.RevokedByIp).HasMaxLength(45);
        builder.Property(token => token.ReplacedByToken).HasMaxLength(200);

        // Veri modeli §1: enum veritabaninda int olarak saklanir.
        builder.Property(token => token.RevokeReason).HasConversion<int?>();

        // Hesaplanan ozellik; kolonu yok, RevokedAt'ten turetilir.
        builder.Ignore(token => token.IsRevoked);

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
