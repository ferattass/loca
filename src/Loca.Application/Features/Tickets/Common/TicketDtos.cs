using Loca.Domain.Enums;

namespace Loca.Application.Features.Tickets.Common;

/// <summary>
/// Kullaniciya gosterilen bilet.
/// </summary>
/// <remarks>
/// Alanlarin cogu biletin kendi satirindan geliyor: bilet kesildigi andaki
/// hâlini saklar, etkinligin adi sonradan degistirilse bile gecmis bilet
/// degismez.
///
/// <para>
/// <b>Mekân ve salon adi bunun disinda,</b> iliski uzerinden okunuyor.
/// Ikisi de fiziksel bir yer: adi degistiyse kapida yazan yeni addir,
/// biletteki eski adi tasimak kullaniciyi yanlis yere gonderirdi.
/// </para>
/// </remarks>
public sealed record TicketDetail(
    Guid Id,
    Guid ReservationId,
    Guid EventId,
    Guid EventSessionId,
    string TicketNumber,
    string QrCode,
    string EventTitle,
    string VenueName,
    string HallName,
    string SeatLabel,
    string TicketTypeName,
    DateTime EventStartsAtUtc,
    decimal Price,
    string Currency,
    TicketStatus Status,
    DateTime IssuedAtUtc);

/// <summary>Kapidaki okutmanin sonucu.</summary>
public static class TicketCheckInVerdict
{
    public const string Admitted = "Admitted";
    public const string AlreadyUsed = "AlreadyUsed";
    public const string NotValid = "NotValid";
}

/// <param name="Admitted">Kisi iceri alinabilir mi.</param>
/// <param name="Verdict">
/// Makine okunur sonuc. Gorevli ekrani karari <see cref="Message"/>
/// metnine degil buna gore veriyor: mesaj metni degistiginde ekranin
/// kirmizi mi yesil mi yanacagi degismemeli.
/// </param>
/// <param name="Status">
/// Biletin okutma sonrasindaki durumu. <see cref="Verdict"/> ile ayni sey
/// degil: iptal edilmis bilet ile bedeli iade edilmis bilet ikisi de
/// <c>NotValid</c> ama gorevli birini gise'ye, digerini iade masasina
/// yonlendirir.
/// </param>
/// <param name="Message">
/// Kayit ve gunluk icin insan okur ozet. Ekranda gosterilecek metin
/// istemcide uretilir; buradaki metin sunucu dilinde ve bicimindedir.
/// </param>
/// <param name="CheckedInAtUtc">
/// Biletin kullanildigi an. Reddedilen okutmada bu alan, biletin DAHA ONCE
/// ne zaman kullanildigini soyler — gorevli "10 dakika once girmis" ile
/// "dun girmis" arasinda farkli davranir.
/// </param>
public sealed record TicketCheckInResult(
    bool Admitted,
    string Verdict,
    TicketStatus Status,
    string Message,
    Guid TicketId,
    string TicketNumber,
    string EventTitle,
    string SeatLabel,
    string TicketTypeName,
    DateTime EventStartsAtUtc,
    DateTime? CheckedInAtUtc);
