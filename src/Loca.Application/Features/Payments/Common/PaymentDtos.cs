using Loca.Domain.Enums;

namespace Loca.Application.Features.Payments.Common;

/// <param name="RedirectUrl">
/// Saglayicinin odeme sayfasi. Taklit saglayicida <c>null</c>; gercek
/// saglayicida kullanici buraya yonlendirilir.
/// </param>
/// <remarks>
/// <b>Kart bilgisi bu akista bizim sunucumuzdan GECMIYOR.</b> Kullanici
/// saglayicinin sayfasinda kartini giriyor, biz yalnizca sonucu goruyoruz.
/// </remarks>
public sealed record PaymentDetail(
    Guid Id,
    Guid ReservationId,
    PaymentStatus Status,
    string Provider,
    string? ProviderReference,
    string? RedirectUrl,
    decimal Amount,
    string Currency,
    DateTime? CompletedAtUtc,
    string? FailureReason,
    DateTime CreatedAt,
    IReadOnlyList<PaymentAttempt> Attempts);

/// <summary>Saglayici geri donusunun isaret ettigi odeme.</summary>
public sealed record ProviderCallbackTarget(Guid PaymentId, Guid ReservationId);

/// <summary>Odeme uzerindeki tek bir saglayici etkilesimi.</summary>
public sealed record PaymentAttempt(
    PaymentTransactionType Type,
    DateTime OccurredAtUtc,
    string? Message);

/// <summary>Odeme tamamlandiktan sonra uretilen bilet.</summary>
public sealed record IssuedTicket(
    Guid Id,
    string TicketNumber,
    string QrCode,
    string EventTitle,
    string SeatLabel,
    string TicketTypeName,
    DateTime EventStartsAtUtc,
    decimal Price,
    string Currency);

/// <param name="StateChanged">
/// Bu cagri durumu degistirdi mi. Tekrar eden callback'te <c>false</c> doner:
/// bilet listesi mevcut biletleri gosterir, yeni bilet uretilmez.
/// </param>
public sealed record PaymentCompletionResult(
    Guid PaymentId,
    PaymentStatus Status,
    Guid ReservationId,
    bool StateChanged,
    IReadOnlyList<IssuedTicket> Tickets);
