using System.Diagnostics;
using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Application.Features.SeatLayouts.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.SeatLayouts.GenerateSeats;

/// <param name="RowLabels">Sira etiketleri: A, B, C...</param>
/// <param name="SeatsPerRow">Her siradaki koltuk sayisi.</param>
/// <param name="OriginY">Bolumun gorsel planda basladigi dikey konum.</param>
public sealed record GenerateSeatsCommand(
    Guid SeatLayoutId,
    Guid SeatSectionId,
    IReadOnlyList<string> RowLabels,
    int SeatsPerRow,
    int HorizontalSpacing,
    int VerticalSpacing,
    int OriginY) : IRequest<Result<GenerateSeatsResponse>>;

public sealed class GenerateSeatsCommandValidator : AbstractValidator<GenerateSeatsCommand>
{
    /// <summary>
    /// Tek istekte uretilebilecek en fazla koltuk.
    /// </summary>
    /// <remarks>
    /// Ust sinir yoksa 1000 sira × 1000 koltuk isteyen tek bir cagri
    /// milyonlarca satir uretmeye calisir ve sunucuyu kilitler. Kabul
    /// testindeki 20 × 30 = 600 bu sinirin cok altinda.
    /// </remarks>
    private const int MaxSeatsPerRequest = 5_000;

    public GenerateSeatsCommandValidator()
    {
        RuleFor(command => command.SeatLayoutId)
            .NotEmpty().WithMessage("Oturma plani zorunludur.");

        RuleFor(command => command.SeatSectionId)
            .NotEmpty().WithMessage("Bolum zorunludur.");

        RuleFor(command => command.RowLabels)
            .NotEmpty().WithMessage("En az bir sira etiketi gerekli.");

        RuleForEach(command => command.RowLabels)
            .NotEmpty().WithMessage("Sira etiketi bos olamaz.")
            .MaximumLength(5).WithMessage("Sira etiketi en fazla 5 karakter olabilir.");

        RuleFor(command => command.RowLabels)
            .Must(labels => labels.Select(label => label.Trim().ToUpperInvariant()).Distinct().Count() == labels.Count)
            .WithMessage("Sira etiketleri benzersiz olmali.")
            .When(command => command.RowLabels is { Count: > 0 });

        RuleFor(command => command.SeatsPerRow)
            .GreaterThan(0).WithMessage("Sira basina koltuk sayisi sifirdan buyuk olmali.");

        RuleFor(command => command)
            .Must(command => (long)command.RowLabels.Count * command.SeatsPerRow <= MaxSeatsPerRequest)
            .WithMessage($"Tek istekte en fazla {MaxSeatsPerRequest} koltuk uretilebilir.")
            .When(command => command.RowLabels is { Count: > 0 } && command.SeatsPerRow > 0);

        RuleFor(command => command.HorizontalSpacing)
            .InclusiveBetween(1, 500).WithMessage("Yatay aralik 1 ile 500 arasinda olmali.");

        RuleFor(command => command.VerticalSpacing)
            .InclusiveBetween(1, 500).WithMessage("Dikey aralik 1 ile 500 arasinda olmali.");

        RuleFor(command => command.OriginY)
            .GreaterThanOrEqualTo(0).WithMessage("Baslangic konumu negatif olamaz.");
    }
}

internal sealed class GenerateSeatsCommandHandler(
    ISeatLayoutRepository seatLayouts,
    IUnitOfWork unitOfWork,
    ILogger<GenerateSeatsCommandHandler> logger)
    : IRequestHandler<GenerateSeatsCommand, Result<GenerateSeatsResponse>>
{
    public async Task<Result<GenerateSeatsResponse>> Handle(
        GenerateSeatsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var layout = await seatLayouts.GetByIdAsync(
            request.SeatLayoutId, koltuklarlaBirlikte: false, cancellationToken);

        if (layout is null)
            return Result.Failure<GenerateSeatsResponse>(SeatLayoutErrors.NotFound);

        var section = await seatLayouts.GetSectionByIdAsync(request.SeatSectionId, cancellationToken);

        if (section is null || section.SeatLayoutId != layout.Id)
            return Result.Failure<GenerateSeatsResponse>(SeatLayoutErrors.SectionNotFound);

        // Ayni bolum icin iki kez calistirilirsa UNIQUE index zaten engeller
        // ama hata veritabanindan donerdi. Burada anlamli bir mesajla kesiliyor.
        if (await seatLayouts.CountSeatsInSectionAsync(section.Id, cancellationToken) > 0)
            return Result.Failure<GenerateSeatsResponse>(SeatLayoutErrors.SectionAlreadyHasSeats);

        var mevcutKoltukSayisi = await seatLayouts.CountSeatsAsync(layout.Id, cancellationToken);
        var uretilecek = request.RowLabels.Count * request.SeatsPerRow;
        var kapasite = layout.Hall?.Capacity ?? 0;

        if (mevcutKoltukSayisi + uretilecek > kapasite)
        {
            logger.LogWarning(
                "Kapasite asimi nedeniyle koltuk uretimi reddedildi. " +
                "PlanId: {PlanId}, Kapasite: {Kapasite}, Mevcut: {Mevcut}, Istenen: {Istenen}",
                layout.Id, kapasite, mevcutKoltukSayisi, uretilecek);

            return Result.Failure<GenerateSeatsResponse>(SeatLayoutErrors.CapacityExceeded);
        }

        var zamanlayici = Stopwatch.StartNew();

        // Uretim domain'de: koltugun nasil yerlestirilecegi bir is kurali,
        // handler'in isi degil.
        var koltuklar = section.GenerateSeats(
            request.RowLabels,
            request.SeatsPerRow,
            request.HorizontalSpacing,
            request.VerticalSpacing,
            request.OriginY);

        // Tek AddRange, tek SaveChanges. Dongude kaydetseydik 600 koltuk
        // 600 ayri gidis donus demek olurdu.
        seatLayouts.AddSeats(koltuklar);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        zamanlayici.Stop();

        logger.LogInformation(
            "Koltuk uretimi tamamlandi. PlanId: {PlanId}, BolumId: {BolumId}, " +
            "Uretilen: {Uretilen}, Sure: {Sure} ms",
            layout.Id, section.Id, koltuklar.Count, zamanlayici.ElapsedMilliseconds);

        return Result.Success(new GenerateSeatsResponse(
            layout.Id,
            section.Id,
            koltuklar.Count,
            mevcutKoltukSayisi + koltuklar.Count));
    }
}
