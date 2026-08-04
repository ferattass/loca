using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Events.Common;
using Loca.Application.Features.Tickets.Common;
using Loca.Domain.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Tickets.CheckInTicket;

/// <summary>
/// Kapida okutulan QR kodunu dogrular ve gecerliyse bileti kullanilmis
/// isaretler.
/// </summary>
/// <remarks>
/// <b>Sonuc hata degil VERI olarak donuyor.</b> Gorevlinin ekraninda
/// "gecebilir" ile "bu bilet 20:14'te kullanilmis" arasindaki fark, sirada
/// bekleyen kalabaligin onunde saniyeler icinde okunmasi gereken bir sey.
/// Reddi HTTP hatasi olarak donseydik istemci ayni ekrani hata yolundan
/// kurmak zorunda kalir, sebebi de govdeden ayiklamasi gerekirdi.
///
/// <para>
/// Yalnizca gercekten hicbir bilete karsilik gelmeyen kod 404 doner.
/// </para>
/// </remarks>
public sealed record CheckInTicketCommand(string QrCode) : IRequest<Result<TicketCheckInResult>>;

internal sealed class CheckInTicketCommandValidator : AbstractValidator<CheckInTicketCommand>
{
    public CheckInTicketCommandValidator()
    {
        RuleFor(komut => komut.QrCode)
            .NotEmpty().WithMessage("QR kodu bos olamaz.")
            .MaximumLength(64).WithMessage("QR kodu en fazla 64 karakter olabilir.");
    }
}

internal sealed class CheckInTicketCommandHandler(
    ITicketRepository tickets,
    IEventRepository events,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CheckInTicketCommand, Result<TicketCheckInResult>>
{
    public async Task<Result<TicketCheckInResult>> Handle(
        CheckInTicketCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bilet = await tickets.GetByQrCodeAsync(request.QrCode.Trim(), cancellationToken);

        if (bilet is null)
            return Result.Failure<TicketCheckInResult>(TicketErrors.QrNotRecognised);

        // Yetki kontrolu verdikten ONCE: yetkisi olmayan biri bileti okutup
        // uzerindeki ad soyad ve koltuk bilgisini gormemeli.
        var etkinlik = await events.GetByIdAsync(bilet.EventId, cancellationToken);

        if (etkinlik is null)
            return Result.Failure<TicketCheckInResult>(TicketErrors.QrNotRecognised);

        if (EventOwnershipGuard.Check(currentUser, etkinlik) is { } yetkiHatasi)
            return Result.Failure<TicketCheckInResult>(yetkiHatasi);

        if (bilet.Status != TicketStatus.Valid)
            return Result.Success(Reddet(bilet));

        try
        {
            bilet.MarkUsed(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Ayni QR baska bir kapida ayni anda okutuldu. Bu bir sistem
            // hatasi degil, tam olarak engellemek istedigimiz durum:
            // gorevliye "bu bilet az once kullanildi" denmeli.
            return Result.Success(new TicketCheckInResult(
                Admitted: false,
                Verdict: TicketCheckInVerdict.AlreadyUsed,
                Status: TicketStatus.Used,
                Message: "Bu bilet az once baska bir kapida okutuldu.",
                TicketId: bilet.Id,
                TicketNumber: bilet.TicketNumber,
                EventTitle: bilet.EventTitle,
                SeatLabel: bilet.SeatLabel,
                TicketTypeName: bilet.TicketTypeName,
                EventStartsAtUtc: bilet.EventStartsAtUtc,
                CheckedInAtUtc: null));
        }

        return Result.Success(new TicketCheckInResult(
            Admitted: true,
            Verdict: TicketCheckInVerdict.Admitted,
            Status: bilet.Status,
            Message: "Giris onaylandi.",
            TicketId: bilet.Id,
            TicketNumber: bilet.TicketNumber,
            EventTitle: bilet.EventTitle,
            SeatLabel: bilet.SeatLabel,
            TicketTypeName: bilet.TicketTypeName,
            EventStartsAtUtc: bilet.EventStartsAtUtc,
            CheckedInAtUtc: bilet.UsedAtUtc));
    }

    /// <remarks>
    /// Reddin sebebi gorevliye acikca soyleniyor: "gecersiz" demek, sirada
    /// bekleyen kisinin bilet gisesine mi yoksa iade masasina mi gitmesi
    /// gerektigini belirsiz birakirdi.
    /// </remarks>
    private static TicketCheckInResult Reddet(Domain.Entities.Ticket bilet) => new(
        Admitted: false,
        Verdict: bilet.Status == TicketStatus.Used
            ? TicketCheckInVerdict.AlreadyUsed
            : TicketCheckInVerdict.NotValid,
        Status: bilet.Status,
        Message: bilet.Status switch
        {
            TicketStatus.Used => "Bu bilet daha once kullanilmis.",
            TicketStatus.Cancelled => "Bu bilet iptal edilmis.",
            TicketStatus.Refunded => "Bu biletin bedeli iade edilmis.",
            _ => "Bu bilet gecerli degil."
        },
        TicketId: bilet.Id,
        TicketNumber: bilet.TicketNumber,
        EventTitle: bilet.EventTitle,
        SeatLabel: bilet.SeatLabel,
        TicketTypeName: bilet.TicketTypeName,
        EventStartsAtUtc: bilet.EventStartsAtUtc,
        CheckedInAtUtc: bilet.UsedAtUtc);
}
