namespace Loca.Domain.Enums;

/// <summary>
/// Etkinligin yasam dongusu.
/// </summary>
/// <remarks>
/// Gecerli akis: Draft → PendingApproval → Published → SalesOpen →
/// SalesClosed → Completed. Cancelled ve Suspended bu akisin disinda,
/// yandan girilen durumlar.
///
/// <para>
/// Durumlar <c>int</c> olarak saklanir ve degerleri elle verilir: sonradan
/// araya yeni bir durum eklendiginde mevcut satirlarin anlami kaymasin.
/// </para>
/// </remarks>
public enum EventStatus
{
    /// <summary>Organizator hazirliyor. Yalnizca sahibi gorur.</summary>
    Draft = 1,

    /// <summary>Admin onayi bekliyor.</summary>
    PendingApproval = 2,

    /// <summary>Onaylandi ve goruntulenebilir, ama bilet satisi baslamadi.</summary>
    Published = 3,

    /// <summary>Bilet satisi acik.</summary>
    SalesOpen = 4,

    /// <summary>Satis kapandi; etkinlik henuz gerceklesmedi.</summary>
    SalesClosed = 5,

    /// <summary>Etkinlik gerceklesti.</summary>
    Completed = 6,

    /// <summary>Iptal edildi. Biletler iade surecine girer.</summary>
    Cancelled = 7,

    /// <summary>Admin tarafindan gecici olarak durduruldu.</summary>
    Suspended = 8
}
