namespace Loca.Domain.Enums;

/// <summary>Ogrenci belgesi dogrulamasinin durumu.</summary>
public enum StudentVerificationStatus
{
    /// <summary>Belge yuklendi, inceleme bekliyor.</summary>
    Pending = 1,

    /// <summary>Dogrulandi; ogrenci bileti alinabilir.</summary>
    Approved = 2,

    /// <summary>Reddedildi; gerekce kayitli.</summary>
    Rejected = 3
}
