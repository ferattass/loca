using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class EventDocumentConfiguration : IEntityTypeConfiguration<EventDocument>
{
    public void Configure(EntityTypeBuilder<EventDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventDocuments");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.Note).HasMaxLength(300);
        builder.Property(document => document.Kind).IsRequired();

        // Etkinligin belgeleri her onay ekraninda birlikte okunuyor.
        builder.HasIndex(document => new { document.EventId, document.Kind });

        // AYNI DOSYA AYNI ETKINLIGE IKI KEZ BAGLANAMAZ. Kullanici "yukle"ye
        // iki kez bastiginda ikinci istek ayni dosya kimligini gonderiyor;
        // kisit olmasaydi onay ekraninda ayni sozlesme iki satir gorunurdu.
        builder.HasIndex(document => new { document.EventId, document.UploadedFileId })
            .IsUnique();

        builder.HasOne(document => document.Event)
            .WithMany()
            .HasForeignKey(document => document.EventId)
            // Etkinlik silinirse belgeleri de gider: belge etkinlikten
            // bagimsiz bir anlam tasimiyor.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(document => document.UploadedFile)
            .WithMany()
            .HasForeignKey(document => document.UploadedFileId)
            // Dosya kaydi silinemez halde tutuluyor: diskteki dosya duruyorken
            // kaydin gitmesi, kime ait oldugu bilinmeyen bir dosya birakirdi.
            .OnDelete(DeleteBehavior.Restrict);

        // Etkinligin kendi soft delete filtresi var (mekan/salon zinciri).
        // Eslesen kosul yazilmazsa silinmis bir mekanin etkinligine ait belge
        // sorgudan donup Event navigasyonu null gelebilir — Gun 4, 5 ve 7'de
        // uc kez yasanan tuzagin ayni tekrari.
        builder.HasQueryFilter(document => !document.Event!.IsDeleted);
    }
}
