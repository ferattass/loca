namespace Loca.Domain.Common;

/// <summary>
/// Ayni satiri iki istek ayni anda guncelledi.
/// </summary>
/// <remarks>
/// Bu tip, ORM'in kendi istisnasi yerine kullaniliyor. Sebep katmanlama:
/// uygulama katmani EF Core'u tanimiyor, dolayisiyla
/// <c>DbUpdateConcurrencyException</c>'i yakalayamaz. Yakalayabilseydi
/// veritabani teknolojisi handler'lara sizardi ve Dapper'a gecis her
/// handler'i etkilerdi.
///
/// <para>
/// Ceviri Persistence katmaninda, <c>SaveChanges</c> sinirinda yapilir.
/// </para>
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Kayit islem sirasinda baskasi tarafindan degistirildi.") { }

    public ConcurrencyConflictException(string message) : base(message) { }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Benzersizlik kisiti ihlal edildi.
/// </summary>
/// <remarks>
/// Kisit adi tasiniyor cunku cagiran taraf hangi kuralin ihlal edildigine
/// gore farkli davraniyor: idempotency anahtari cakismasi "ayni istek iki
/// kez geldi" demek ve mevcut kayit donulmeli; koltuk tekilligi cakismasi
/// ise gercek bir yaris ve 409 donulmeli. Ikisi ayni istisna tipiyle gelip
/// ayirt edilemeseydi tekrar gonderilen bir istek hata olarak donerdi.
/// </remarks>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
        : base("Benzersizlik kurali ihlal edildi.") { }

    public UniqueConstraintViolationException(string message) : base(message) { }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException) { }

    public UniqueConstraintViolationException(
        string message, string? constraintName, Exception innerException)
        : base(message, innerException) => ConstraintName = constraintName;

    /// <summary>Veritabanindaki kisit/index adi. Bilinmiyorsa <c>null</c>.</summary>
    public string? ConstraintName { get; }
}
