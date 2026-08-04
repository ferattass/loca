namespace Loca.WebApi.Contracts.Tickets;

/// <summary>
/// Kapida okutulan QR kodu.
/// </summary>
/// <remarks>
/// Kod URL'de degil GOVDEDE tasiniyor. Sorgu dizesindeki bir deger sunucu
/// erisim kayitlarina, tarayici gecmisine ve araya giren vekil sunucularin
/// loglarina duz metin olarak yazilir; bu kod kapida gecerli olan seyin
/// kendisi.
/// </remarks>
public sealed record CheckInTicketRequest(string QrCode);
