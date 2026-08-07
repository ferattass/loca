using Loca.Application.Common.Models;

namespace Loca.Application.Features.Payments.Common;

public static class PaymentErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Payment.NotFound", "Odeme bulunamadi.");

    public static readonly Error Unauthenticated =
        Error.Unauthorized("Payment.Unauthenticated", "Bu islem icin giris yapmalisiniz.");

    public static readonly Error NotOwner =
        Error.Forbidden("Payment.NotOwner", "Bu odeme size ait degil.");

    public static readonly Error ReservationNotFound =
        Error.NotFound("Payment.ReservationNotFound", "Rezervasyon bulunamadi.");

    /// <remarks>
    /// Suresi dolmus rezervasyonun odemesi baslatilamaz: koltuklar bu arada
    /// baskasina gitmis olabilir. Baslatilsaydi para tahsil edilir ama
    /// karsiliginda verilecek koltuk bulunmazdi.
    /// </remarks>
    public static readonly Error ReservationNotActive =
        Error.Conflict(
            "Payment.ReservationNotActive",
            "Rezervasyonun suresi dolmus veya iptal edilmis. Odeme baslatilamaz.");

    public static readonly Error AlreadyPaid =
        Error.Conflict(
            "Payment.AlreadyPaid",
            "Bu rezervasyonun odemesi zaten tamamlanmis.");

    /// <remarks>
    /// Ayni rezervasyon icin ikinci bir odeme baslatilmasi engelleniyor;
    /// aksi hâlde kullanici iki kez tahsil edilebilirdi.
    /// </remarks>
    public static readonly Error AlreadyPending =
        Error.Conflict(
            "Payment.AlreadyPending",
            "Bu rezervasyon icin sonuclanmamis bir odeme var.");

    public static readonly Error ProviderRejected =
        Error.Conflict("Payment.ProviderRejected", "Odeme saglayicisi islemi reddetti.");

    public static readonly Error NotRefundable =
        Error.Conflict("Payment.NotRefundable", "Yalnizca basarili odeme iade edilebilir.");

    /// <remarks>
    /// Bilet uretimi ile odeme kaydi ayni transaction'da yaziliyor. Bu hata
    /// gorunuyorsa ikisi ayrismis demektir; sessizce ikinci kez bilet
    /// uretmek yerine acikca hata veriliyor.
    /// </remarks>
    public static readonly Error TicketsAlreadyIssued =
        Error.Conflict(
            "Payment.TicketsAlreadyIssued",
            "Bu rezervasyonun biletleri zaten uretilmis.");

    public static readonly Error SeatsNoLongerHeld =
        Error.Conflict(
            "Payment.SeatsNoLongerHeld",
            "Rezervasyonun koltuklari artik tutulmuyor. Odeme tamamlanamaz.");

    /// <remarks>
    /// Havale panelden kapatilmis ya da banka bilgileri eksik. Eksik
    /// bilgiyle acilan bir havale odemesi, kullaniciya "havale ile ode"
    /// deyip nereye odeyecegini soylememek olurdu.
    /// </remarks>
    public static readonly Error BankTransferDisabled =
        Error.Conflict(
            "Payment.BankTransferDisabled",
            "Havale ile odeme su anda kapali.");

    /// <remarks>
    /// Tamamlama ucu sonucu saglayiciya sorarak dogruluyor; havalenin
    /// sorulacak bir saglayicisi yok, onayi yonetici veriyor. Bu kontrol
    /// olmasaydi kullanici havale kaydi icin tamamlama cagirir ve o an
    /// calisan saglayici "basarili" deyip parasi gelmemis bir rezervasyona
    /// bilet uretirdi.
    /// </remarks>
    public static readonly Error BankTransferNotCompletable =
        Error.Conflict(
            "Payment.BankTransferNotCompletable",
            "Havale odemesi buradan tamamlanamaz; odeme yonetim tarafindan onaylanir.");

    /// <remarks>Yonetici onayi yalnizca havale kayitlari icin anlamli.</remarks>
    public static readonly Error NotBankTransfer =
        Error.Conflict(
            "Payment.NotBankTransfer",
            "Bu odeme havale ile yapilmamis; elle onaylanamaz.");

    public static readonly Error NotPending =
        Error.Conflict("Payment.NotPending", "Bu odeme zaten sonuclanmis.");
}
