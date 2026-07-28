namespace Loca.Domain.Common;

/// <summary>
/// Bir kullaniciya ait olan kaynak. Yetkilendirmenin ucuncu seviyesi
/// (kaynak sahipligi) bu arayuz uzerinden calisir.
/// </summary>
/// <remarks>
/// Analiz belgesindeki yetki matrisinde "kendi etkinligini guncelleme" ile
/// "baskasinin etkinligini guncelleme" ayri satirlar. Bu ayrim rol kontrolüyle
/// cozulemez: iki kullanici da Organizer rolunde. Karar calisma aninda
/// kaynagin sahibine bakilarak verilir.
///
/// <para>
/// Gun 5'te <c>Event</c>, Gun 6'da <c>Reservation</c>, Gun 7'de <c>Ticket</c>
/// bu arayuzu uygulayacak; yetkilendirme kodu degismeyecek.
/// </para>
/// </remarks>
public interface IOwnedResource
{
    Guid OwnerId { get; }
}
