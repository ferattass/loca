namespace Loca.Domain.Enums;

/// <summary>
/// Bir odemenin yasam dongusu.
/// </summary>
/// <remarks>
/// Degerler elle veriliyor: araya yeni bir durum eklendiginde mevcut
/// satirlarin anlami kaymasin.
/// </remarks>
public enum PaymentStatus
{
    /// <summary>Baslatildi, saglayicidan sonuc bekleniyor.</summary>
    Pending = 1,

    /// <summary>Tahsil edildi. Biletler bu durumda uretilir.</summary>
    Succeeded = 2,

    /// <summary>Saglayici reddetti veya hata verdi. Koltuklar serbest kalir.</summary>
    Failed = 3,

    /// <summary>Tahsil edilmis tutar iade edildi.</summary>
    Refunded = 4,

    /// <summary>Sonuclanmadan iptal edildi (kullanici vazgecti, sure doldu).</summary>
    Cancelled = 5
}

/// <summary>
/// Odeme kaydi uzerindeki her saglayici etkilesiminin turu.
/// </summary>
/// <remarks>
/// Odemenin kendi durumu yalnizca SON hâli gosterir. Denemelerin dokumu
/// ayri satirlarda tutuluyor: "odeme neden basarisiz oldu, kac kez denendi,
/// saglayici ne dondu" sorularinin cevabi tek bir durum alaninda duramaz.
/// </remarks>
public enum PaymentTransactionType
{
    Create = 1,
    Complete = 2,
    Fail = 3,
    Refund = 4,
    Cancel = 5
}

/// <summary>Biletin durumu.</summary>
public enum TicketStatus
{
    /// <summary>Gecerli, girise kabul edilir.</summary>
    Valid = 1,

    /// <summary>Girişte okutuldu. Ikinci kez kullanilamaz.</summary>
    Used = 2,

    /// <summary>Etkinlik iptal edildi veya rezervasyon iptal oldu.</summary>
    Cancelled = 3,

    /// <summary>Bedeli iade edildi.</summary>
    Refunded = 4
}
