using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using Loca.Domain.Entities;
using Loca.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Loca.Persistence.Queries;

/// <summary>
/// Yonetim panelinin okuma sorgulari.
/// </summary>
internal sealed class AdminQueries(LocaDbContext context) : IAdminQueries
{
    public async Task<AdminOzeti> GetOverviewAsync(
        DateTime gunBasiUtc,
        DateTime utcNow,
        string activePaymentProvider,
        bool redisAvailable,
        CancellationToken cancellationToken = default)
    {
        // Odemeler duruma gore tek sorguda gruplaniyor. Basarili, iade ve
        // basarisiz icin ayri COUNT atilsaydi ayni tablo uc kez taranirdi.
        var odemeGruplari = await context.Payments
            .Where(payment => payment.CreatedAt >= gunBasiUtc && payment.CreatedAt < utcNow)
            .GroupBy(payment => payment.Status)
            .Select(grup => new
            {
                Durum = grup.Key,
                Adet = grup.Count(),
                Tutar = grup.Sum(payment => payment.Amount.Amount)
            })
            .ToListAsync(cancellationToken);

        var kuyrukGruplari = await context.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .GroupBy(message => message.RetryCount)
            .Select(grup => new { Deneme = grup.Key, Adet = grup.Count() })
            .ToListAsync(cancellationToken);

        var biletSayisi = await context.Tickets
            .CountAsync(ticket => ticket.IssuedAtUtc >= gunBasiUtc, cancellationToken);

        var bekleyenRezervasyon = await context.Reservations
            .CountAsync(
                reservation =>
                    reservation.Status == ReservationStatus.Pending &&
                    reservation.ExpiresAtUtc > utcNow,
                cancellationToken);

        var yaklasanOturum = await context.EventSessions
            .CountAsync(session => session.StartsAtUtc > utcNow, cancellationToken);

        int Adet(PaymentStatus durum) =>
            odemeGruplari.Where(g => g.Durum == durum).Sum(g => g.Adet);

        decimal Tutar(PaymentStatus durum) =>
            odemeGruplari.Where(g => g.Durum == durum).Sum(g => g.Tutar);

        return new AdminOzeti(
            GeneratedAtUtc: utcNow,
            Today: new GunlukSatis(
                SucceededCount: Adet(PaymentStatus.Succeeded),
                TotalAmount: Tutar(PaymentStatus.Succeeded),
                RefundedCount: Adet(PaymentStatus.Refunded),
                RefundedAmount: Tutar(PaymentStatus.Refunded),
                FailedCount: Adet(PaymentStatus.Failed),
                // Sistem tek para birimiyle calisiyor; coklu para birimi
                // geldiginde bu alanin gruplamaya girmesi gerekecek.
                Currency: "TRY"),
            Queue: new KuyrukDurumu(
                Pending: kuyrukGruplari.Where(g => g.Deneme == 0).Sum(g => g.Adet),
                Retryable: kuyrukGruplari
                    .Where(g => g.Deneme > 0 && g.Deneme < OutboxMessage.MaxRetryCount)
                    .Sum(g => g.Adet),
                DeadLettered: kuyrukGruplari
                    .Where(g => g.Deneme >= OutboxMessage.MaxRetryCount)
                    .Sum(g => g.Adet)),
            Health: new SistemSagligi(Database: true, Redis: redisAvailable),
            ActivePaymentProvider: activePaymentProvider,
            TicketsIssuedToday: biletSayisi,
            PendingReservations: bekleyenRezervasyon,
            UpcomingSessions: yaklasanOturum);
    }

    public async Task<PagedResult<AdminOdemeSatiri>> GetPaymentsAsync(
        AdminOdemeFiltresi filtre, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);

        var sorgu = context.Payments.AsQueryable();

        if (filtre.Status is { } durum)
            sorgu = sorgu.Where(payment => payment.Status == durum);

        if (filtre.FromUtc is { } baslangic)
            sorgu = sorgu.Where(payment => payment.CreatedAt >= baslangic);

        if (filtre.ToUtc is { } bitis)
            sorgu = sorgu.Where(payment => payment.CreatedAt < bitis);

        if (!string.IsNullOrWhiteSpace(filtre.Search))
        {
            var arama = filtre.Search.Trim();

            // Tam GUID yazildiysa dogrudan kimlikten aranir: yonetici bir
            // hata kaydindaki odeme kimligini kopyalayip yapistirdiginda
            // metin aramasinda hicbir sey bulamazdi.
            if (Guid.TryParse(arama, out var kimlik))
            {
                sorgu = sorgu.Where(payment =>
                    payment.Id == kimlik || payment.ReservationId == kimlik);
            }
            else
            {
                var kalip = $"%{arama}%";

                sorgu = sorgu.Where(payment =>
                    EF.Functions.ILike(payment.User!.FullName, kalip) ||
                    EF.Functions.ILike(payment.User!.Email, kalip) ||
                    (payment.ProviderReference != null &&
                        EF.Functions.ILike(payment.ProviderReference, kalip)));
            }
        }

        var toplam = await sorgu.CountAsync(cancellationToken);

        if (toplam == 0)
            return PagedResult<AdminOdemeSatiri>.Empty(
                filtre.Pagination.PageNumber, filtre.Pagination.PageSize);

        var satirlar = await sorgu
            // Yeniden eskiye: yonetici neredeyse her zaman az once olan bir
            // seye bakiyor.
            .OrderByDescending(payment => payment.CreatedAt)
            .Skip((filtre.Pagination.PageNumber - 1) * filtre.Pagination.PageSize)
            .Take(filtre.Pagination.PageSize)
            .Select(payment => new AdminOdemeSatiri(
                payment.Id,
                payment.ReservationId,
                payment.Status,
                payment.Provider,
                payment.ProviderReference,
                payment.Amount.Amount,
                payment.Amount.Currency,
                payment.User!.FullName,
                payment.User!.Email,
                payment.Reservation!.EventSession!.Event!.Title,
                payment.Reservation!.EventSession!.StartsAtUtc,
                payment.Reservation!.Items.Count,
                payment.CompletedAtUtc,
                payment.FailureReason,
                payment.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminOdemeSatiri>
        {
            Items = satirlar,
            PageNumber = filtre.Pagination.PageNumber,
            PageSize = filtre.Pagination.PageSize,
            TotalCount = toplam
        };
    }

    public async Task<PagedResult<AdminKullanici>> GetUsersAsync(
        AdminKullaniciFiltresi filtre, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);

        var sorgu = context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtre.Search))
        {
            var kalip = $"%{filtre.Search.Trim()}%";

            sorgu = sorgu.Where(user =>
                EF.Functions.ILike(user.FullName, kalip) ||
                EF.Functions.ILike(user.Email, kalip));
        }

        if (!string.IsNullOrWhiteSpace(filtre.Role))
        {
            var rol = filtre.Role.Trim();

            sorgu = sorgu.Where(user =>
                user.UserRoles.Any(userRole => userRole.Role!.Name == rol));
        }

        var toplam = await sorgu.CountAsync(cancellationToken);

        if (toplam == 0)
            return PagedResult<AdminKullanici>.Empty(
                filtre.Pagination.PageNumber, filtre.Pagination.PageSize);

        var satirlar = await sorgu
            .OrderByDescending(user => user.CreatedAt)
            .Skip((filtre.Pagination.PageNumber - 1) * filtre.Pagination.PageSize)
            .Take(filtre.Pagination.PageSize)
            .Select(user => new AdminKullanici(
                user.Id,
                user.FullName,
                user.Email,
                user.EmailConfirmed,
                user.UserRoles.Select(userRole => userRole.Role!.Name).ToList(),
                // Alt sorgu olarak ceviriliyor; iliskileri yuklemeye gerek yok.
                context.Reservations.Count(reservation => reservation.UserId == user.Id),
                context.Tickets.Count(ticket => ticket.UserId == user.Id),
                user.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminKullanici>
        {
            Items = satirlar,
            PageNumber = filtre.Pagination.PageNumber,
            PageSize = filtre.Pagination.PageSize,
            TotalCount = toplam
        };
    }

    public async Task<IReadOnlyList<KuyrukMesaji>> GetQueueAsync(
        string durum, int limit, CancellationToken cancellationToken = default)
    {
        var sorgu = durum switch
        {
            "Processed" => context.OutboxMessages
                .Where(message => message.ProcessedAtUtc != null),
            "Retryable" => context.OutboxMessages
                .Where(message =>
                    message.ProcessedAtUtc == null &&
                    message.RetryCount > 0 &&
                    message.RetryCount < OutboxMessage.MaxRetryCount),
            "DeadLettered" => context.OutboxMessages
                .Where(message =>
                    message.ProcessedAtUtc == null &&
                    message.RetryCount >= OutboxMessage.MaxRetryCount),
            _ => context.OutboxMessages
                .Where(message => message.ProcessedAtUtc == null && message.RetryCount == 0)
        };

        return await sorgu
            .OrderByDescending(message => message.OccurredAtUtc)
            .Take(limit)
            // Govde TASINMIYOR, yalnizca uzunlugu: yuk e-posta adresi gibi
            // kisisel veri iceriyor ve yonetim ekraninda gosterilmesi o
            // veriyi tarayici gecmisine ve ekran goruntulerine tasirdi.
            .Select(message => new KuyrukMesaji(
                message.Id,
                message.Type,
                message.Payload.Length,
                message.RetryCount,
                message.ErrorMessage,
                message.CorrelationId,
                message.OccurredAtUtc,
                message.ProcessedAtUtc))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
