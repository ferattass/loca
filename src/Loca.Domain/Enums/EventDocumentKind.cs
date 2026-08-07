namespace Loca.Domain.Enums;

/// <summary>
/// Etkinlige eklenen belgenin turu.
/// </summary>
/// <remarks>
/// Tur serbest metin degil enum: onay ekrani "sahne sozlesmesi var mi"
/// sorusunu ancak sabit bir degere bakarak cevaplayabilir. Metin olsaydi
/// "kira sozlesmesi", "sozlesme", "sahne belgesi" gibi yazimlarin hepsi
/// ayri sayilir ve zorunluluk kontrolu hicbir zaman tutmazdi.
/// </remarks>
public enum EventDocumentKind
{
    /// <summary>Sahnenin/salonun o tarih icin tutuldugunu gosteren sozlesme.</summary>
    VenueContract = 1,

    /// <summary>Resmi izin yazisi (belediye, valilik, telif).</summary>
    Permit = 2,

    /// <summary>Digerleri: sigorta, teknik sartname, sanatci sozlesmesi.</summary>
    Other = 3,
}
