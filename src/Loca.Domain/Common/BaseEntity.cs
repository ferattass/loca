namespace Loca.Domain.Common;

/// <summary>
/// Tum varliklarin ortak temeli. Audit alanlari
/// Persistence katmanindaki interceptor tarafindan otomatik doldurulur;
/// handler icinde elle atanmaz.
/// </summary>
public abstract class BaseEntity
{
    /// <remarks>
    /// Guid v7 zaman sirali uretilir. Rastgele Guid'in aksine
    /// index parcalanmasina yol acmaz — 28 tabloluk bir semada fark hissedilir.
    /// </remarks>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Fiziksel silinmeyecek varliklar. Gecmis satis kayitlari bunlara
/// referans verdigi icin silme islemi isaretleme olarak yapilir.
/// Sorgulardan dusmesi global query filter ile saglanir.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Tutarlilik siniri. Bir aggregate root uzerinden yapilan degisiklikler
/// tek transaction icinde kaydedilir.
/// </summary>
public interface IAggregateRoot;
