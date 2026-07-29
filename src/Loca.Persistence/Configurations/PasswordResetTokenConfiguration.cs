using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration
    : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PasswordResetTokens");
        builder.HasKey(token => token.Id);

        // SHA-256'nin onaltilik gosterimi her zaman 64 karakter.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        // Sifirlama istegi bu kolonla aranir; ayrica ayni ozetten iki kayit
        // olusmasi mumkun olmamali.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.RequestedByIp)
            .HasMaxLength(45);

        // Kullanicinin acik token'larini bulmak icin (yeni istek gelince
        // oncekiler gecersiz kilinir).
        builder.HasIndex(token => new { token.UserId, token.UsedAt });

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
