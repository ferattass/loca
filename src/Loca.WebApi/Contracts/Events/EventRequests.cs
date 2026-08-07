using Loca.Domain.Enums;

namespace Loca.WebApi.Contracts.Events;

/// <remarks>
/// Istek modelleri command'lerden AYRI. Ayni olsalardi rota parametresi
/// (etkinlik kimligi) govdede de tasinmak zorunda kalir ve ikisi
/// celistiginde hangisinin gecerli oldugu belirsiz olurdu. Ayrica
/// organizator kimligi gibi token'dan gelen alanlarin govdeye sizmasi
/// engelleniyor.
/// </remarks>
public sealed record CreateEventRequest(
    Guid CategoryId,
    string Title,
    string Description,
    string CancellationPolicy,
    Guid CityId,
    Guid VenueId,
    Guid HallId,
    DateTime EventDateUtc,
    int DurationMinutes,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    int? MinimumAge);

public sealed record UpdateEventRequest(
    Guid CategoryId,
    string Title,
    string Description,
    string CancellationPolicy,
    int? MinimumAge,
    Guid? CityId,
    Guid? VenueId,
    Guid? HallId,
    DateTime? EventDateUtc,
    int? DurationMinutes,
    DateTime? SalesStartsAtUtc,
    DateTime? SalesEndsAtUtc);

public sealed record CreateEventSessionRequest(
    Guid HallId,
    Guid SeatLayoutId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc);

public sealed record CancelEventRequest(string Reason);

public sealed record SetEventPosterRequest(Guid? PosterFileId);

public sealed record CreateTicketTypeRequest(
    string Name,
    decimal Price,
    string Currency,
    int Quota,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc,
    bool RequiresVerification,
    Guid? SeatSectionId);

public sealed record UpdateTicketTypeRequest(
    string Name,
    decimal Price,
    string Currency,
    int Quota,
    DateTime SalesStartsAtUtc,
    DateTime SalesEndsAtUtc);

public sealed record AssignSectionRequest(Guid? SeatSectionId);

/// <param name="UploadedFileId">
/// Once <c>POST /files/belge</c> ile yuklenen dosyanin kimligi. Dosya
/// icerigi burada TASINMIYOR: iki adim ayri, yukleme basarili olup baglanti
/// basarisiz olursa etkinlik kaydi bozulmuyor.
/// </param>
public sealed record AddEventDocumentRequest(
    Guid UploadedFileId,
    EventDocumentKind Kind,
    string? Note);
