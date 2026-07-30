using Loca.Domain.Common;

namespace Loca.Application.Common.Authorization;

/// <summary>
/// Kaynak sahipligi kurali — ucuncu seviye yetkilendirmenin cekirdegi.
/// </summary>
/// <remarks>
/// Kural iki yerden cagriliyor: WebApi'deki <c>ResourceOwnerAuthorizationHandler</c>
/// (policy ile korunan uclar icin) ve handler'lar (kaynagi kendisi yukleyen
/// use case'ler icin). Ikisi de ayni metoda gidiyor.
///
/// <para>
/// Iki ayri yerde tekrar yazilsaydi biri "admin de erisebilir" satirini
/// atlar ve ayni sistemde iki farkli yetki kurali olusurdu — bu tur bir
/// ayrisma testle yakalanmadigi surece sessizdir.
/// </para>
///
/// <para>
/// Kabul olcutu: A'nin token'iyla B'nin kaynagina istek <b>403</b> donmeli;
/// 200 de 404 de degil. 404 donmek "kaynak yok" demek olurdu ve saldirgan
/// kaynagin varligini olcerek bilgi toplayabilirdi... ama 403 de varligi
/// dogruluyor. Burada bilinçli tercih 403: organizator panelinde kendi
/// olmayan bir kaynaga tikladigi anda "yetkin yok" gormesi, "kayit yok"
/// gormesinden anlasilir.
/// </para>
/// </remarks>
public static class Ownership
{
    /// <param name="userId">Istegi yapan kullanici. Anonim istekte <c>null</c>.</param>
    /// <param name="isAdmin">
    /// Admin her kaynaga erisir: yetki matrisinde "baskasinin etkinligini
    /// yonetme" yalnizca admin satirinda arti.
    /// </param>
    public static bool Allows(Guid? userId, bool isAdmin, IOwnedResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return isAdmin || (userId is { } id && id == resource.OwnerId);
    }
}
