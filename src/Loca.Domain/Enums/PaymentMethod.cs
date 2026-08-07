namespace Loca.Domain.Enums;

/// <summary>
/// Odemenin hangi yolla yapildigi.
/// </summary>
/// <remarks>
/// <b>Saglayici adindan AYRI bir alan.</b> <c>Payment.Provider</c> "isi kim
/// yapti" sorusunun cevabi (Iyzico, Mock); bu alan "para nasil geldi"
/// sorusunun. Ikisi ayni sayilsaydi havale odemesi de bir saglayici adi
/// tasimak zorunda kalirdi ve "saglayiciya sor" yolundan gecebilirdi —
/// oysa havalenin dogrulamasi insan.
/// </remarks>
public enum PaymentMethod
{
    /// <summary>Saglayicinin odeme sayfasindan kart ile.</summary>
    Card = 1,

    /// <summary>Banka havalesi/EFT; onayi yonetici veriyor.</summary>
    BankTransfer = 2,
}
