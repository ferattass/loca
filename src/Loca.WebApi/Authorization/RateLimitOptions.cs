using System.ComponentModel.DataAnnotations;

namespace Loca.WebApi.Authorization;

/// <summary>
/// Hiz sinirlarinin yapilandirmadan okunan degerleri.
/// </summary>
/// <remarks>
/// <b>Neden kod icinde sabit degil:</b> uretim degerleri (kimlik ucunda
/// dakikada 10 istek) uctan uca kabul betiklerini kiriyor — yaris testi
/// tek IP'den 50 kullanici kaydediyor ve bu, gercek bir kullanicinin asla
/// yapmayacagi ama testin yapmak ZORUNDA oldugu bir sey.
///
/// <para>
/// Sinirlari gelistirmede tamamen kapatmak yanlis olurdu: o zaman
/// sinirlama yalnizca uretimde calisir ve hicbir yerde denenmemis olurdu.
/// Ayni tuzaga odeme saglayicisinda dusulmustu — test edilen yol ile
/// uretimde calisacak yol ayni degildi (bkz. 31 numarali sorun). Bunun
/// yerine gelistirmede degerler GENIS, guvenlik kabul betigi ise API'yi
/// dar degerlerle ayaga kaldirip siniri gercekten asiyor.
/// </para>
/// </remarks>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Giris, kayit, token yenileme.</summary>
    public RateLimitKurali Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Sifre sifirlama istegi — her cagri bir e-posta gonderiyor.</summary>
    public RateLimitKurali PasswordReset { get; set; } = new() { PermitLimit = 3, WindowSeconds = 900 };

    /// <summary>Butun uclar icin kaba tavan.</summary>
    public RateLimitKurali Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
}

public sealed class RateLimitKurali
{
    [Range(1, 100_000)]
    public int PermitLimit { get; set; }

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
