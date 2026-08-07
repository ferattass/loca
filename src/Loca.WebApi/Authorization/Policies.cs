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

    /// <summary>Kullanici yonetimi, odeme ayarlari, kategori/mekan yonetimi.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Onay kuyrugu: etkinlik ve organizator basvurularini inceleyip karara baglama.
    /// </summary>
    /// <remarks>
    /// <c>AdminOnly</c>'den ayri bir policy, cunku onay isi buyudukce tek
    /// admin hesabiyla yurumuyor ama onay verecek herkese admin yetkisi
    /// vermek odeme ayarlarini ve rol atamayi da acmak olurdu. Admin bu
    /// policy'ye de dahil: onay yetkisi admin'in zaten sahip oldugu
    /// yetkilerin alt kumesi.
    /// </remarks>
    public const string ModeratorOnly = "ModeratorOnly";

    /// <summary>Ucuncu seviye: kaynagin sahibi mi (veya admin mi).</summary>
    public const string ResourceOwner = "ResourceOwner";
}
