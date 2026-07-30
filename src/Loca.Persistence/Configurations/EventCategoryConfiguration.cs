using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class EventCategoryConfiguration : IEntityTypeConfiguration<EventCategory>
{
    /// <remarks>
    /// Sehir tohumlamasindaki gerekcenin aynisi: kimlikler ve zaman damgasi
    /// sabit verilir, aksi hâlde migration her uretildiginde EF farkli deger
    /// gorup yeni satirlar ekler.
    /// </remarks>
    private static readonly DateTime SeededAt = new(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<EventCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventCategories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(category => category.Name).IsUnique();

        builder.Property(category => category.Slug)
            .IsRequired()
            .HasMaxLength(100);

        // Adres cubugunda kullanildigi icin tekil olmali: iki kategori ayni
        // slug'a sahip olsaydi /kategori/tiyatro hangisini acacagi belirsizdi.
        builder.HasIndex(category => category.Slug).IsUnique();

        builder.Property(category => category.Description)
            .HasMaxLength(500);

        builder.Property(category => category.IsActive)
            .HasDefaultValue(true);

        // Kategori tablosu bos oldugunda etkinlik hic olusturulamaz:
        // CategoryId zorunlu ve Restrict ile bagli.
        builder.HasData(
            Seed("22222222-0000-0000-0000-000000000001", "Konser", "konser"),
            Seed("22222222-0000-0000-0000-000000000002", "Tiyatro", "tiyatro"),
            Seed("22222222-0000-0000-0000-000000000003", "Stand-up", "stand-up"),
            Seed("22222222-0000-0000-0000-000000000004", "Konferans", "konferans"),
            Seed("22222222-0000-0000-0000-000000000005", "Festival", "festival"),
            Seed("22222222-0000-0000-0000-000000000006", "Çocuk", "cocuk"));
    }

    private static object Seed(string id, string name, string slug) => new
    {
        Id = new Guid(id),
        Name = name,
        Slug = slug,
        IsActive = true,
        CreatedAt = SeededAt
    };
}
