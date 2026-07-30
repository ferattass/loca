namespace Loca.Domain.Enums;

/// <summary>
/// Organizatör olma basvurusunun durumu.
/// </summary>
/// <remarks>
/// Basvuru ile profil ayri tutuluyor: basvuru reddedilse bile kaydi kalir
/// (ayni kisi ikinci kez basvurdugunda gecmisi gorunur), profil ise yalnizca
/// onaylanmis organizatorde olusur.
/// </remarks>
public enum OrganizerApplicationStatus
{
    /// <summary>Admin incelemesi bekliyor.</summary>
    Pending = 1,

    /// <summary>Onaylandi; profil olusturuldu ve Organizer rolu verildi.</summary>
    Approved = 2,

    /// <summary>Reddedildi; gerekce kayitli.</summary>
    Rejected = 3
}
