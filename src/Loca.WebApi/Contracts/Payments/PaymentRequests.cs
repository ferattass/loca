using Loca.Domain.Enums;

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
/// <param name="Method">
/// Verilmezse kart. Havale yalnizca panelden acikken ve banka bilgileri
/// doluyken kabul ediliyor; kapaliyken 409 doner.
/// </param>
public sealed record StartPaymentRequest(Guid ReservationId, PaymentMethod? Method = null);

public sealed record RefundPaymentRequest(string? Reason);

/// <param name="Reference">Ekstredeki islem numarasi. Zorunlu degil.</param>
public sealed record ConfirmBankTransferRequest(string? Reference);

/// <param name="Reason">
/// Zorunlu: koltuklari geri alan ve musteriye bildirim giden bir karar,
/// gerekcesiz kayda gecmemeli.
/// </param>
public sealed record RejectBankTransferRequest(string Reason);
