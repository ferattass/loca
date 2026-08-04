using Loca.Application.Common.Models;
using Loca.Domain.Enums;

namespace Loca.Application.Features.Admin.Common;

/// <param name="TotalAmount">
/// Satilan tutar. Iade edilen tutar BU RAKAMDAN DUSULMEZ, ayri gosterilir:
/// "bugun ne kadar sattik" ile "bugun ne kadar iade ettik" farkli sorular
/// ve tek bir net rakama indirilirse iade oraninin arttigi fark edilmez.
/// </param>
public sealed record GunlukSatis(
    int SucceededCount,
    decimal TotalAmount,
    int RefundedCount,
    decimal RefundedAmount,
    int FailedCount,
    string Currency);

/// <param name="Pending">Hic denenmemis, islenmeyi bekleyen.</param>
/// <param name="Retryable">En az bir kez basarisiz olmus, hakki bitmemis.</param>
/// <param name="DeadLettered">
/// Hakki tukenmis. <b>Sifirdan buyukse birinin bakmasi gerekiyor:</b> bu
/// mesajlari is akisi bir daha ele almiyor, yani birinin e-postasi hic
/// gitmemis olabilir.
/// </param>
public sealed record KuyrukDurumu(int Pending, int Retryable, int DeadLettered);

/// <param name="Redis">
/// Kilit altyapisi. <c>false</c> sistemin durdugu anlamina GELMEZ; koltuk
/// kilitleme katmanli savunmanin ilk halkasini kaybeder ve veritabani
/// savunmasiyla devam eder. Yine de gorunur olmasi gerekiyor.
/// </param>
public sealed record SistemSagligi(bool Database, bool Redis);

/// <summary>Yonetim panelinin acilis ekrani.</summary>
public sealed record AdminOzeti(
    DateTime GeneratedAtUtc,
    GunlukSatis Today,
    KuyrukDurumu Queue,
    SistemSagligi Health,
    string ActivePaymentProvider,
    int TicketsIssuedToday,
    int PendingReservations,
    int UpcomingSessions);

/// <summary>Kuyruktaki tek bir mesaj.</summary>
/// <remarks>
/// <b>Govde tasinmiyor, yalnizca uzunlugu.</b> Outbox yuku e-posta adresi
/// gibi kisisel veri iceriyor; yonetim ekraninda gosterilmesi o veriyi
/// tarayici gecmisine ve ekran goruntulerine tasirdi.
/// </remarks>
public sealed record KuyrukMesaji(
    Guid Id,
    string Type,
    int PayloadLength,
    int RetryCount,
    string? ErrorMessage,
    Guid? CorrelationId,
    DateTime OccurredAtUtc,
    DateTime? ProcessedAtUtc);

/// <summary>Yonetim listesindeki tek bir odeme.</summary>
/// <remarks>
/// Kullanicinin adi ve e-postasi listede duruyor: yoneticinin bir odemeyi
/// aradigi durum neredeyse her zaman "su kisi arayip sikayet etti" ile
/// basliyor ve kimlik numarasiyla arama yapilamiyor.
///
/// <para>
/// <b>Saglayici referansi tasiniyor</b> cunku mutabakat sirasinda
/// saglayicinin panelindeki kayitla eslestirmenin tek yolu bu.
/// </para>
/// </remarks>
public sealed record AdminOdemeSatiri(
    Guid Id,
    Guid ReservationId,
    PaymentStatus Status,
    string Provider,
    string? ProviderReference,
    decimal Amount,
    string Currency,
    string UserFullName,
    string UserEmail,
    string EventTitle,
    DateTime SessionStartsAtUtc,
    int SeatCount,
    DateTime? CompletedAtUtc,
    string? FailureReason,
    DateTime CreatedAt);

/// <param name="Status">Verilirse yalnizca o durumdakiler.</param>
/// <param name="Search">
/// Kullanici adi, e-postasi veya saglayici referansinda aranir. Odeme
/// kimligiyle de aranabilmesi icin tam GUID de kabul ediliyor.
/// </param>
public sealed record AdminOdemeFiltresi(
    PaymentStatus? Status,
    string? Search,
    DateTime? FromUtc,
    DateTime? ToUtc,
    PaginationRequest Pagination);

/// <summary>Yonetim listesindeki tek bir kullanici.</summary>
public sealed record AdminKullanici(
    Guid Id,
    string FullName,
    string Email,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    int ReservationCount,
    int TicketCount,
    DateTime CreatedAt);

/// <param name="Role">Verilirse yalnizca o role sahip olanlar.</param>
public sealed record AdminKullaniciFiltresi(
    string? Search,
    string? Role,
    PaginationRequest Pagination);
