namespace Loca.Infrastructure.Payments;

// Iyzico ile konusulan tel sozlesmesi: yalnizca serilestirilen/ayristirilan
// veri siniflari. Saglayici dosyasindan AYRILDILAR cunku burada is mantigi
// yok — degisme sebepleri de farkli: bu dosya Iyzico'nun API'si degistiginde,
// digeri odeme akisi degistiginde degisir.
//
// YANIT SINIFLARI KASITLI OLARAK DAR: yalnizca status, errorCode,
// errorMessage ve islem kimlikleri tanimli. Kart maskesi, BIN, kart tipi
// gibi alanlar Iyzico'nun yanitinda gelse bile burada karsiligi olmadigi
// icin nesneye tasinmiyor, dolayisiyla loglara da sizamiyor.

// --- Istek govdeleri --------------------------------------------------------
// Bu siniflar yalnizca serilestirilir (Iyzico'ya gonderilir); "required" ile
// isaretli alanlar sayesinde eksik bir zorunlu deger unutulursa derleme
// zamaninda yakalanir.

internal sealed class CheckoutFormInitializeRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string Price { get; init; }
    public required string PaidPrice { get; init; }
    public required string Currency { get; init; }
    public required string BasketId { get; init; }
    public required string PaymentGroup { get; init; }
    public required string CallbackUrl { get; init; }
    public required Buyer Buyer { get; init; }
    public required IyzicoAddress ShippingAddress { get; init; }
    public required IyzicoAddress BillingAddress { get; init; }
    public required List<BasketItem> BasketItems { get; init; }
}

internal sealed class Buyer
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required string GsmNumber { get; init; }
    public required string Email { get; init; }
    public required string IdentityNumber { get; init; }
    public required string RegistrationAddress { get; init; }
    public required string Ip { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
}

internal sealed class IyzicoAddress
{
    public required string ContactName { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public required string Address { get; init; }
}

internal sealed class BasketItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category1 { get; init; }
    public required string ItemType { get; init; }
    public required string Price { get; init; }
}

internal sealed class CheckoutFormRetrieveRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string Token { get; init; }
}

internal sealed class RefundRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string PaymentTransactionId { get; init; }
    public required string Price { get; init; }
    public required string Ip { get; init; }
}

internal sealed class CancelRequest
{
    public required string Locale { get; init; }
    public required string ConversationId { get; init; }
    public required string PaymentId { get; init; }
    public required string Ip { get; init; }
}

// --- Yanit govdeleri ---------------------------------------------------------
// Kasitli olarak dar: Iyzico'nun asil yanitinda bulunabilecek kart maskesi,
// BIN, kart tipi gibi alanlar burada hic tanimlanmadi. System.Text.Json
// taninmayan alanlari sessizce yok sayar; bu yuzden o alanlar JSON'da gelse
// bile bu nesnelere hicbir zaman tasinmaz ve yanlislikla loglanamaz.

internal sealed class CheckoutFormInitializeResponse
{
    public string? Status { get; init; }
    public string? Token { get; init; }

    /// <summary>Kullanicinin karti girecegi Iyzico sayfasi.</summary>
    public string? PaymentPageUrl { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class CheckoutFormRetrieveResponse
{
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public string? PaymentId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ItemTransaction>? ItemTransactions { get; init; }
}

internal sealed class ItemTransaction
{
    public string? PaymentTransactionId { get; init; }
}

internal sealed class RefundResponse
{
    public string? Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class CancelResponse
{
    public string? Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
