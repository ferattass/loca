namespace Loca.Domain.Constants;

/// <summary>
/// Davranisa etki eden veritabani kisit adlari.
/// </summary>
/// <remarks>
/// Yalnizca <b>koda gore dallanilan</b> kisitlar burada. Bir kisit ihlali
/// yakalandiginda hangi kural oldugunu anlamanin tek yolu adina bakmak;
/// ad iki yerde (EF konfigurasyonu ve hata yakalayan handler) ayri ayri
/// yazilsaydi, birini yeniden adlandiran kisi digerini sessizce bozardi ve
/// idempotency cakismasi "beklenmeyen hata" olarak disari cikardi.
/// </remarks>
public static class DatabaseConstraints
{
    /// <summary>
    /// <c>UNIQUE(UserId, IdempotencyKey)</c> — ayni istegin iki kez
    /// islenmesini veritabani seviyesinde engeller.
    /// </summary>
    public const string ReservationIdempotencyKey = "IX_Reservations_UserId_IdempotencyKey";

    /// <summary>
    /// <c>UNIQUE(EventSessionId, SeatId)</c> — yaris durumunun son savunma hatti.
    /// </summary>
    public const string EventSeatSessionSeat = "IX_EventSeats_EventSessionId_SeatId";
}
