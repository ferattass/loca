namespace Loca.WebApi.Contracts.Payments;

/// <remarks>
/// <b>Tutar alani bilerek yok.</b> Odenecek tutar rezervasyonun kendi
/// toplamindan kopyalanir; istekte tasinsaydi araya giren biri dokuz yuz
/// liralik rezervasyonu bir liraya odeyebilirdi.
///
/// <para>
/// Kart bilgisi de yok: kullanici karti saglayicinin sayfasinda giriyor.
/// </para>
/// </remarks>
public sealed record StartPaymentRequest(Guid ReservationId);

public sealed record RefundPaymentRequest(string? Reason);
