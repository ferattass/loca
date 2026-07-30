namespace Loca.Domain.Common;

/// <summary>
/// Etkinligin gerceklesecegi yer: sehir, mekan ve salon.
/// </summary>
/// <remarks>
/// Uc kimlik birlikte anlam tasiyor ve birlikte dogrulanmali. Ayri
/// parametreler olarak dolassaydi cagri yerinde siralarinin karismasi
/// derleyiciye gorunmez bir hata olurdu — ucu de <c>Guid</c>.
///
/// <para>
/// Sehrin gercekten mekanin sehri olup olmadigi burada dogrulanamaz;
/// o kontrol veritabani sorgusu gerektirir ve handler'da yapilir. Burada
/// yalnizca "uc alan da dolu" garantisi var.
/// </para>
/// </remarks>
public readonly record struct EventPlace
{
    public EventPlace(Guid cityId, Guid venueId, Guid hallId)
    {
        if (cityId == Guid.Empty)
            throw new DomainException("Etkinlik bir sehre bagli olmali.");

        if (venueId == Guid.Empty)
            throw new DomainException("Etkinlik bir mekana bagli olmali.");

        if (hallId == Guid.Empty)
            throw new DomainException("Etkinlik bir salona bagli olmali.");

        CityId = cityId;
        VenueId = venueId;
        HallId = hallId;
    }

    public Guid CityId { get; }
    public Guid VenueId { get; }
    public Guid HallId { get; }
}
