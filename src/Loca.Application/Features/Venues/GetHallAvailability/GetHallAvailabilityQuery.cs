using FluentValidation;
using Loca.Application.Common.Models;
using Loca.Domain.Entities;
using MediatR;

namespace Loca.Application.Features.Venues.GetHallAvailability;

/// <param name="CleanupBufferMinutes">
/// Iki oturum arasinda birakilmasi gereken pay. Arayuz "dolu" derken bunu
/// da yaziyor: kullanici salonun bos gorundugu bir saate yazip reddedilince
/// sebebi anlamiyordu.
/// </param>
public sealed record SalonDoluluk(
    bool IsAvailable,
    int CleanupBufferMinutes,
    IReadOnlyList<DoluAralik> Conflicts);

/// <param name="EventTitle">
/// Cakisan etkinligin adi. <b>Yayinlanmamis etkinliklerde de donuyor:</b>
/// salonun o saatte tutulu oldugu bilgisi, tutanin kim oldugundan bagimsiz
/// olarak gerekli. Baska bir organizatorun taslagi da salonu bloke ediyor.
/// </param>
public sealed record DoluAralik(
    Guid EventSessionId,
    string EventTitle,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

/// <summary>
/// Bir salonun verilen aralikta musait olup olmadigi.
/// </summary>
/// <remarks>
/// <b>Kaydetmeden once sorulabilsin diye var.</b> Cakisma kontrolu zaten
/// oturum eklenirken yapiliyor ama ancak "Kaydet"e basildiktan sonra:
/// organizator formu bastan sona doldurup gonderiyor ve 409 aliyordu.
/// Bu uc ayni kurali onceden sorduruyor.
///
/// <para>
/// Kontrolun KENDISI burada tekrarlanmiyor — ayni depo metodu cagriliyor.
/// Kopyalansaydi iki kural zamanla ayrisir, ekran "musait" derken kayit
/// reddedilirdi.
/// </para>
/// </remarks>
public sealed record GetHallAvailabilityQuery(
    Guid HallId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    Guid? ExcludeEventId = null) : IRequest<Result<SalonDoluluk>>;

internal sealed class GetHallAvailabilityQueryValidator
    : AbstractValidator<GetHallAvailabilityQuery>
{
    public GetHallAvailabilityQueryValidator()
    {
        RuleFor(sorgu => sorgu.HallId).NotEmpty();

        RuleFor(sorgu => sorgu.EndsAtUtc)
            .GreaterThan(sorgu => sorgu.StartsAtUtc)
            .WithMessage("Bitis, baslangictan sonra olmali.");
    }
}

/// <summary>
/// Salon doluluk sorgusunun okuma tarafi.
/// </summary>
/// <remarks>
/// Depo (<c>IEventRepository</c>) yalnizca "cakisma var mi" diye
/// cevapliyor; ekranin ihtiyaci olan ise HANGI oturumla cakistigi. Okuma
/// icin ayri bir arayuz, Gun 5'te <c>IEventQueries</c> ile kurulan
/// ayrimin devami.
/// </remarks>
public interface IHallAvailabilityQueries
{
    Task<IReadOnlyList<DoluAralik>> GetConflictsAsync(
        Guid hallId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? excludeEventId,
        CancellationToken cancellationToken = default);
}

internal sealed class GetHallAvailabilityQueryHandler(IHallAvailabilityQueries queries)
    : IRequestHandler<GetHallAvailabilityQuery, Result<SalonDoluluk>>
{
    public async Task<Result<SalonDoluluk>> Handle(
        GetHallAvailabilityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cakisanlar = await queries.GetConflictsAsync(
            request.HallId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.ExcludeEventId,
            cancellationToken);

        return Result.Success(new SalonDoluluk(
            IsAvailable: cakisanlar.Count == 0,
            CleanupBufferMinutes: (int)EventSession.TemizlikPayi.TotalMinutes,
            Conflicts: cakisanlar));
    }
}
