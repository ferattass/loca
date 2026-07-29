namespace Loca.Persistence.Common;

/// <summary>
/// <c>ILIKE</c> kaliplari icin kacis islemleri.
/// </summary>
/// <remarks>
/// Buyuk/kucuk harf duyarsiz karsilastirma <c>ToLower()</c> ile de yazilabilirdi
/// ama iki sorunu var: cevrilen SQL kolon uzerinde fonksiyon cagirdigi icin
/// index kullanilamaz, ve <c>ToLower()</c> calisan makinenin kulturune baglidir —
/// Turkce kulturde "I" harfi "ı" olur ve "ISTANBUL" ile "istanbul" beklenmedik
/// bicimde farkli sayilabilir. PostgreSQL'in <c>ILIKE</c>'i bu isi veritabani
/// tarafinda, kulturden bagimsiz yapar.
/// </remarks>
internal static class LikePattern
{
    /// <summary>
    /// Kalipta ozel anlam tasiyan karakterleri kacirir.
    /// </summary>
    /// <remarks>
    /// Kacirilmasaydi adinda <c>%</c> gecen bir kayit ararken kalip
    /// "her sey" anlamina gelir ve alakasiz kayitlar eslesirdi.
    /// </remarks>
    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Ters bolu once kacirilir; sonra kacirilsaydi diger karakterler
        // icin eklenen kacis isaretleri de ikinci kez kacirilirdi.
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    /// <summary>Icinde gecme aramasi icin kalip: <c>%deger%</c>.</summary>
    internal static string Contains(string value) => $"%{Escape(value.Trim())}%";
}
