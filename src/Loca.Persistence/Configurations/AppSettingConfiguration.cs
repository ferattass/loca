using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loca.Persistence.Configurations;

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AppSettings");
        builder.HasKey(setting => setting.Id);

        builder.Property(setting => setting.Key)
            .HasMaxLength(100)
            .IsRequired();

        // Sifrelenmis deger duz metinden uzun; Data Protection ciktisi
        // base64 ve giris uzunluguna gore buyuyor. 2000 karakter, uzun bir
        // API anahtari sifrelendiginde bile rahat siginir.
        builder.Property(setting => setting.Value)
            .HasMaxLength(2000);

        // Ayni anahtarin iki satiri olamaz: hangisinin gecerli oldugu
        // belirsiz kalirdi ve okuma sirasi veritabaninin keyfine kalirdi.
        builder.HasIndex(setting => setting.Key)
            .IsUnique()
            .HasDatabaseName("IX_AppSettings_Key");
    }
}
