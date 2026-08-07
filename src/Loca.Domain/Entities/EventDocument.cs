using Loca.Domain.Common;
using Loca.Domain.Enums;

namespace Loca.Domain.Entities;

/// <summary>
/// Etkinlige eklenen resmi belge: sahne kira sozlesmesi, izin yazisi.
/// </summary>
/// <remarks>
/// <b>Neden ayri bir varlik, afis gibi tek alan degil:</b> bir etkinlik icin
/// birden fazla belge geliyor (sozlesme + izin + sigorta) ve her birinin
/// turu, notu ve kimin yukledigi ayri ayri gerekiyor. Tek alan olsaydi
/// ikinci belge birincinin uzerine yazilirdi.
///
/// <para>
/// Belgeler <b>herkese acik degil</b>: yalnizca etkinligin sahibi ve onay
/// ekibi goruyor. Kira sozlesmesi ticari sir ve imza tasiyor; vitrinde
/// gorunmesi icin hicbir sebep yok.
/// </para>
/// </remarks>
public sealed class EventDocument : BaseEntity
{
    private EventDocument()
    {
    }

    public EventDocument(
        Guid eventId,
        Guid uploadedFileId,
        EventDocumentKind kind,
        Guid uploadedByUserId,
        string? note)
    {
        if (eventId == Guid.Empty)
            throw new DomainException("Belge bir etkinlige bagli olmali.");

        if (uploadedFileId == Guid.Empty)
            throw new DomainException("Belge bir dosyaya bagli olmali.");

        if (uploadedByUserId == Guid.Empty)
            throw new DomainException("Belgeyi yukleyen belli olmali.");

        if (note is { Length: > 300 })
            throw new DomainException("Belge notu 300 karakteri asamaz.");

        EventId = eventId;
        UploadedFileId = uploadedFileId;
        Kind = kind;
        UploadedByUserId = uploadedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid EventId { get; private set; }
    public Event? Event { get; private set; }

    public Guid UploadedFileId { get; private set; }
    public UploadedFile? UploadedFile { get; private set; }

    public EventDocumentKind Kind { get; private set; }

    /// <summary>Belgeyi yukleyen. Denetimde "kim koydu" sorusunun cevabi.</summary>
    public Guid UploadedByUserId { get; private set; }

    /// <summary>Organizatorun aciklamasi. Ornegin "3. salon, 12 Agustos".</summary>
    public string? Note { get; private set; }
}
