namespace Loca.Domain.Enums;

/// <summary>
/// Bir rezervasyonun yasam dongusu.
/// </summary>
/// <remarks>
/// <c>Cancelled</c> ile <c>Expired</c> bilerek ayri tutuluyor: ikisi de
/// koltuklari serbest birakir ama sebepleri farkli. Kullanici vazgectiginde
/// <c>Cancelled</c>, sure dolduğunda <c>Expired</c> yazilir; raporda "kac
/// kisi vazgecti" ile "kac kisi odemeyi yetistiremedi" ayni sayiya
/// karisirsa kilit suresinin kisa olup olmadigi anlasilamaz.
/// </remarks>
public enum ReservationStatus
{
    /// <summary>Koltuklar kilitli, odeme bekleniyor.</summary>
    Pending = 1,

    /// <summary>Odeme tamamlandi, biletler uretildi (Gun 7).</summary>
    Confirmed = 2,

    /// <summary>Kullanici vazgecti veya odeme basarisiz oldu.</summary>
    Cancelled = 3,

    /// <summary>Kilit suresi doldu, koltuklar geri birakildi.</summary>
    Expired = 4
}
