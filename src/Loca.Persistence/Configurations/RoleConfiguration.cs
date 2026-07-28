using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Tohumlanan rol kimlikleri. Sabit deger kullanilir cunku her ortamda
    /// (gelistirme, test, teslim) ayni kimlikler olusmali; rastgele uretilseydi
    /// migration her calistiginda yeni satir eklerdi.
    /// </summary>
    private static readonly Guid CustomerRoleId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizerRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminRoleId = new("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Tohum kayitlarinin olusturulma zamani. Interceptor migration sirasinda
    /// calismadigi icin acikca verilir; sabit olmasi gerekir, aksi hâlde her
    /// migration uretiminde model degismis gorunur.
    /// </summary>
    private static readonly DateTime SeededAt = new(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(role => role.Name).IsUnique();

        builder.Property(role => role.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasData(
            new
            {
                Id = CustomerRoleId,
                Name = RoleNames.Customer,
                Description = "Etkinlik goruntuleyen, rezervasyon yapan ve bilet alan kullanici.",
                CreatedAt = SeededAt
            },
            new
            {
                Id = OrganizerRoleId,
                Name = RoleNames.Organizer,
                Description = "Kendi etkinliklerini olusturan, yayinlayan ve raporlarini goren kullanici.",
                CreatedAt = SeededAt
            },
            new
            {
                Id = AdminRoleId,
                Name = RoleNames.Admin,
                Description = "Sistem yoneticisi. Kullanici, mekan, kategori ve basvuru yonetimi.",
                CreatedAt = SeededAt
            });
    }
}
