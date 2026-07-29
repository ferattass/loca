using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    /// <remarks>
    /// Rol tohumlamasindaki gerekcenin aynisi: kimlikler sabit verilir,
    /// aksi hâlde migration her uretildiginde yeni satirlar olusur.
    /// </remarks>
    private static readonly DateTime SeededAt = new(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<City> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Cities");
        builder.HasKey(city => city.Id);

        builder.Property(city => city.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(city => city.Name).IsUnique();

        builder.Property(city => city.PlateCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.HasIndex(city => city.PlateCode).IsUnique();

        builder.Property(city => city.IsActive)
            .HasDefaultValue(true);

        // Sartname bes sehirlik bir baslangic verisi istiyor. Etkinlik
        // listelemesinin ana filtresi bu tablo; bos oldugunda mekan bile
        // olusturulamaz.
        builder.HasData(
            Seed("11111111-0000-0000-0000-000000000034", "İstanbul", "34"),
            Seed("11111111-0000-0000-0000-000000000006", "Ankara", "06"),
            Seed("11111111-0000-0000-0000-000000000035", "İzmir", "35"),
            Seed("11111111-0000-0000-0000-000000000016", "Bursa", "16"),
            Seed("11111111-0000-0000-0000-000000000007", "Antalya", "07"));
    }

    private static object Seed(string id, string name, string plateCode) => new
    {
        Id = new Guid(id),
        Name = name,
        PlateCode = plateCode,
        IsActive = true,
        CreatedAt = SeededAt
    };
}
