namespace Loca.WebApi.Authorization;

/// <summary>
/// Yetkilendirme policy adlari — ikinci seviye.
/// </summary>
/// <remarks>
/// Neden dogrudan <c>[Authorize(Roles = "Organizer,Admin")]</c> yazilmiyor?
/// Cunku o metin her endpoint'te tekrar eder ve bir gun "admin de organizator
/// islemlerini yapabilsin" karari degistiginde yirmi yerde duzeltme gerekir.
/// Policy tek yerde tanimlanir, endpoint yalnizca adini soyler.
/// </remarks>
public static class Policies
{
    /// <summary>Etkinlik olusturma, bilet turu tanimlama, kendi raporlari.</summary>
    public const string OrganizerOnly = "OrganizerOnly";

    /// <summary>Kullanici yonetimi, basvuru onaylama, kategori/mekan yonetimi.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Ucuncu seviye: kaynagin sahibi mi (veya admin mi).</summary>
    public const string ResourceOwner = "ResourceOwner";
}
