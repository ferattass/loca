namespace Loca.Persistence.Seeding;

/// <summary>
/// Ilk admin hesabinin bilgileri. Roller migration icinde tohumlanir
/// (<c>HasData</c>) ama admin hesabi tohumlanmaz.
/// </summary>
/// <remarks>
/// Sebep: migration dosyalari depoya girer ve depo herkese acik. Sifre ozeti
/// <c>HasData</c> ile yazilsaydi BCrypt ozeti bile olsa her ortamda ayni
/// sabit sifre gecerli olurdu ve depoyu klonlayan herkes admin girisi
/// yapabilirdi. Bu yuzden hesap calisma aninda, sifre yapilandirmadan
/// okunarak olusturulur: gelistirmede user-secrets, konteynerde
/// <c>AdminSeed__Password</c> ortam degiskeni.
/// </remarks>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = "Sistem Yoneticisi";
}
