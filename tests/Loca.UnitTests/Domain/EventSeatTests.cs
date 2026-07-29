using Loca.Domain.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;

namespace Loca.UnitTests.Domain;

/// <summary>
/// Koltugun bir oturumdaki durum gecisleri.
/// </summary>
/// <remarks>
/// Gun 6'daki rezervasyon akisi bu kurallarin uzerine kurulacak. Buradaki
/// bir hata "ayni koltuk iki kisiye satildi" demek oldugu icin gecisler
/// tek tek testle sabitlendi.
/// </remarks>
public class EventSeatTests
{
    private static readonly DateTime Simdi = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(10);

    private static EventSeat YeniKoltuk() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), new Money(500, "TRY"));

    [Fact]
    public void NewSeatShouldBeAvailable()
    {
        var koltuk = YeniKoltuk();

        Assert.Equal(EventSeatStatus.Available, koltuk.Status);
        Assert.True(koltuk.IsAvailable(Simdi));
    }

    [Fact]
    public void LockShouldSetOwnerAndDeadline()
    {
        var koltuk = YeniKoltuk();
        var kullanici = Guid.CreateVersion7();

        koltuk.Lock(kullanici, Simdi, KilitSuresi);

        Assert.Equal(EventSeatStatus.Locked, koltuk.Status);
        Assert.Equal(kullanici, koltuk.LockedByUserId);
        Assert.Equal(Simdi.Add(KilitSuresi), koltuk.LockedUntilUtc);
    }

    [Fact]
    public void LockedSeatCannotBeLockedByAnotherUser()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);

        // Kilit suresi dolmadan ikinci kullanici alamaz.
        Assert.Throws<DomainException>(() =>
            koltuk.Lock(Guid.CreateVersion7(), Simdi.AddMinutes(5), KilitSuresi));
    }

    /// <remarks>
    /// Suresi dolmus kilit bos sayilir. Sayilmasaydi odemesini tamamlamayan
    /// bir kullanici yuzunden koltuk kalici olarak satilamaz hale gelirdi.
    /// </remarks>
    [Fact]
    public void ExpiredLockShouldAllowNewLock()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);

        var sonra = Simdi.Add(KilitSuresi).AddSeconds(1);

        Assert.True(koltuk.IsLockExpired(sonra));
        Assert.True(koltuk.IsAvailable(sonra));

        var ikinciKullanici = Guid.CreateVersion7();
        koltuk.Lock(ikinciKullanici, sonra, KilitSuresi);

        Assert.Equal(ikinciKullanici, koltuk.LockedByUserId);
    }

    [Fact]
    public void ExpiredLockCannotBeExtended()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);

        var sonra = Simdi.Add(KilitSuresi).AddSeconds(1);

        Assert.Throws<DomainException>(() => koltuk.ExtendLock(sonra, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ExtendLockShouldPushDeadline()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);

        koltuk.ExtendLock(Simdi.AddMinutes(9), TimeSpan.FromMinutes(5));

        Assert.Equal(Simdi.AddMinutes(15), koltuk.LockedUntilUtc);
    }

    [Fact]
    public void SeatMustBeLockedBeforeReservation()
    {
        var koltuk = YeniKoltuk();

        Assert.Throws<DomainException>(() => koltuk.AttachToReservation(Guid.CreateVersion7()));
    }

    [Fact]
    public void SeatMustBeReservedBeforeSold()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);

        // Kilitliden dogrudan satisa gecilemez: arada odemenin baglandigi
        // rezervasyon adimi var.
        Assert.Throws<DomainException>(koltuk.MarkSold);
    }

    [Fact]
    public void ReservationToSoldShouldClearLock()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);
        koltuk.AttachToReservation(Guid.CreateVersion7());
        koltuk.MarkSold();

        Assert.Equal(EventSeatStatus.Sold, koltuk.Status);
        Assert.Null(koltuk.LockedUntilUtc);
        Assert.Null(koltuk.LockedByUserId);
    }

    [Fact]
    public void SoldSeatCannotBeReleased()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);
        koltuk.AttachToReservation(Guid.CreateVersion7());
        koltuk.MarkSold();

        Assert.Throws<DomainException>(koltuk.Release);
    }

    [Fact]
    public void ReleaseShouldClearReservationAndLock()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);
        koltuk.AttachToReservation(Guid.CreateVersion7());

        koltuk.Release();

        Assert.Equal(EventSeatStatus.Available, koltuk.Status);
        Assert.Null(koltuk.ReservationId);
        Assert.Null(koltuk.LockedByUserId);
        Assert.Null(koltuk.LockedUntilUtc);
    }

    [Fact]
    public void SoldSeatCannotBeDisabled()
    {
        var koltuk = YeniKoltuk();
        koltuk.Lock(Guid.CreateVersion7(), Simdi, KilitSuresi);
        koltuk.AttachToReservation(Guid.CreateVersion7());
        koltuk.MarkSold();

        Assert.Throws<DomainException>(koltuk.Disable);
    }

    /// <remarks>
    /// Fiyat bilet turunden kopyalanir, referans verilmez: bilet turunun
    /// fiyati sonradan degistiginde satilmis biletin tutari degismemeli.
    /// </remarks>
    [Fact]
    public void PriceShouldBeCopiedAtCreation()
    {
        var fiyat = new Money(750.505m, "try");
        var koltuk = new EventSeat(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), fiyat);

        Assert.Equal(750.50m, koltuk.Price.Amount);
        Assert.Equal("TRY", koltuk.Price.Currency);
    }
}
