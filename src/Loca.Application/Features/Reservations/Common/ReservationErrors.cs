using Loca.Application.Common.Models;

namespace Loca.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon akisinin beklenen hatalari.
/// </summary>
/// <remarks>
/// Burada tipli hata donulmesinin sebebi Gun 5'te ogrenilen derse dayaniyor:
/// ayni durum kodu iki farkli sebepten gelirse test yesil kalirken urun
/// bozuk olabiliyor. Ozellikle 409'lar birbirinden <c>code</c> alaniyla
/// ayriliyor — istemci "koltuk az once alindi" ile "bilet limitini astiniz"
/// durumlarina farkli tepki vermek zorunda.
/// </remarks>
public static class ReservationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Reservation.NotFound", "Rezervasyon bulunamadi.");

    public static readonly Error Unauthenticated =
        Error.Unauthorized("Reservation.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    public static readonly Error NotOwner =
        Error.Forbidden("Reservation.NotOwner", "Bu rezervasyon size ait degil.");

    public static readonly Error SessionNotFound =
        Error.NotFound("Reservation.SessionNotFound", "Oturum bulunamadi.");

    public static readonly Error EventNotPublished =
        Error.Conflict(
            "Reservation.EventNotPublished",
            "Bu etkinlik henuz satista degil.");

    public static readonly Error SessionCancelled =
        Error.Conflict("Reservation.SessionCancelled", "Bu oturum iptal edilmis.");

    public static readonly Error SeatsNotGenerated =
        Error.Conflict(
            "Reservation.SeatsNotGenerated",
            "Bu oturumun koltuklari henuz uretilmedi.");

    public static readonly Error SalesNotStarted =
        Error.Conflict("Reservation.SalesNotStarted", "Bu oturumun bilet satisi henuz baslamadi.");

    public static readonly Error SalesEnded =
        Error.Conflict("Reservation.SalesEnded", "Bu oturumun bilet satisi kapandi.");

    /// <remarks>
    /// Kullanicinin ayni oturumdaki diger aktif rezervasyonlari da sayilir;
    /// yalnizca istekteki koltuk sayisina bakilsaydi kullanici arka arkaya
    /// alti kez bir koltuk alarak limiti asardi.
    /// </remarks>
    public static Error SeatLimitExceeded(int limit, int mevcut) =>
        Error.Conflict(
            "Reservation.SeatLimitExceeded",
            $"Bir oturumda en fazla {limit} bilet alabilirsiniz. Su anda {mevcut} biletiniz var.");

    public static readonly Error SeatNotInSession =
        Error.NotFound(
            "Reservation.SeatNotInSession",
            "Secilen koltuklardan bazilari bu oturuma ait degil.");

    /// <remarks>
    /// Istemci bu kodu gorunce koltuk planini yenilemeli: kullanici artik
    /// var olmayan bir secimle bakiyor.
    /// </remarks>
    public static readonly Error SeatNotAvailable =
        Error.Conflict(
            "Reservation.SeatNotAvailable",
            "Secilen koltuklardan bazilari az once alindi. Lutfen koltuk planini yenileyin.");

    /// <remarks>
    /// Yukaridakiyle ayni HTTP kodunu doner ama sebebi farklidir: koltuk
    /// okundugunda bostu, yazilirken baskasi almisti. Ayri kod, yarisin
    /// hangi katmanda yakalandigini logda gorunur kiliyor.
    /// </remarks>
    public static readonly Error SeatTakenConcurrently =
        Error.Conflict(
            "Reservation.SeatTakenConcurrently",
            "Koltuklar islem sirasinda baskasi tarafindan alindi. Lutfen tekrar deneyin.");

    public static readonly Error StudentVerificationRequired =
        Error.Conflict(
            "Reservation.StudentVerificationRequired",
            "Secilen bilet turu ogrenci dogrulamasi gerektiriyor. " +
            "Onaylanmis ve suresi gecerli bir ogrenci belgeniz bulunmuyor.");
}
