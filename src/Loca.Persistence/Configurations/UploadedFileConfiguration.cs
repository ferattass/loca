using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UploadedFiles");
        builder.HasKey(file => file.Id);

        builder.Property(file => file.FileName)
            .IsRequired()
            .HasMaxLength(100);

        // Diskteki ad benzersiz uretilir; index bunu veritabani seviyesinde
        // de garanti eder. Ayni ada iki kayit dusmesi, bir dosyanin digerinin
        // uzerine yazildigi anlamina gelirdi.
        builder.HasIndex(file => file.FileName).IsUnique();

        builder.Property(file => file.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(file => file.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(file => file.RelativePath)
            .IsRequired()
            .HasMaxLength(500);
    }
}
