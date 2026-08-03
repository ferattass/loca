namespace Loca.WebApi.Contracts.Reservations;

/// <summary>
/// Rezervasyon olusturma istegi.
/// </summary>
/// <remarks>
/// <b>Tutar alani BILEREK YOK.</b> Toplam her zaman sunucuda, koltuklarin
/// kendi satirlarindaki fiyatlardan hesaplanir. Istekte tasinsaydi araya
/// giren biri 450 TL'lik koltugu 1 TL'ye rezerve edebilirdi.
///
/// <para>
/// <c>Idempotency-Key</c> de gövdede degil BASLIKTA tasiniyor: anahtar
/// istegin icerigi degil, istegin kimligi. Govdede olsaydi ayni islemin
/// tekrari icin govdenin de birebir ayni kurulmasi gerekirdi.
/// </para>
/// </remarks>
public sealed record CreateReservationRequest(Guid EventSessionId, IReadOnlyList<Guid> EventSeatIds);
