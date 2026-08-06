namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Calisma aninda degistirilebilen ayarlar.
/// </summary>
/// <remarks>
/// <b>Yapilandirmanin (appsettings / user-secrets) yerine gecmez, ustune
/// biner.</b> Bir ayar burada tanimliysa o kullanilir; degilse
/// yapilandirmadaki deger gecerlidir. Boylece panelden hic dokunulmamis
/// bir kurulum bugunku gibi calismaya devam ediyor.
///
/// <para>
/// Okuma cok sik (her e-posta gonderiminde, her rezervasyonda), yazma
/// nadir. Bu yuzden degerler onbellege aliniyor ve yalnizca yazmada
/// dusuruluyor; her cagride veritabanina gitmek, ayarlari kod icinde
/// sabitlemekten daha pahali bir cozum olurdu.
/// </para>
/// </remarks>
public interface ISettingsStore
{
    /// <summary>
    /// Tum ayarlar. Sir olanlarin degeri <b>cozulmus</b> hâlde doner;
    /// yalnizca sunucu ici kullanim icindir.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Tek bir ayar; tanimli degilse <c>null</c>.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ayarlari topluca yazar.
    /// </summary>
    /// <param name="secrets">
    /// Sir olarak saklanacak anahtarlar. Bu anahtarlarin degeri
    /// sifrelenerek yazilir ve okuma uclarindan hicbir zaman donmez.
    /// <b>Bos gonderilen bir sir yazilmaz, mevcut deger korunur</b> —
    /// panel sirri hic gostermedigi icin form her acildiginda o alan bos
    /// geliyor ve bos degeri yazsaydik baska bir alani duzelten yonetici
    /// sirri farkinda olmadan silerdi.
    /// </param>
    /// <param name="clearedSecrets">
    /// Silinecek sirlar. "Bos = koru" kuralinin acik istisnasi: bu
    /// listedeki anahtarin degeri <c>null</c>'a cekilir.
    /// <b>Bu olmadan kaydedilmis bir sir hicbir zaman kaldirilamazdi</b> —
    /// kimlik dogrulamali bir sunucudan dogrulamasiz birine gecen yonetici
    /// eski sifreyi veritabaninda birakmak zorunda kalirdi.
    /// </param>
    /// <remarks>
    /// Toplu yazma: bir formdaki dort alan tek tek yazilsaydi, ucuncu
    /// yazmada olusan bir hata yarim yapilandirilmis bir SMTP birakirdi.
    /// </remarks>
    Task SetAsync(
        IReadOnlyDictionary<string, string?> values,
        IReadOnlySet<string> secrets,
        IReadOnlySet<string>? clearedSecrets = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sir degerleri sifreler ve cozer.
/// </summary>
/// <remarks>
/// Uygulamasi ASP.NET Core Data Protection uzerinde. Anahtar dosyalari
/// uygulamanin dosya sisteminde durur; veritabani yedegini eline geciren
/// biri sifreleri cozemez.
///
/// <para>
/// Bu, sirlarin veritabaninda tutulmasini <b>ideal</b> yapmaz — en
/// dogrusu bir sir yoneticisi (Key Vault, Secrets Manager). Duz metin
/// saklamaya gore acik farkla iyi ve staj projesinin altyapisiyla
/// yapilabilir olan bu.
/// </para>
/// </remarks>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <returns>Cozulemezse <c>null</c>: anahtar degismis olabilir.</returns>
    string? Unprotect(string protectedValue);
}
