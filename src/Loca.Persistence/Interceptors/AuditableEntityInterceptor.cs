using Loca.Application.Common.Interfaces;
using Loca.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loca.Persistence.Interceptors;

/// <summary>
/// Audit alanlarini kayit sirasinda otomatik doldurur.
/// </summary>
/// <remarks>
/// Bu is handler'lara birakilsaydi er ya da gec biri unuturdu ve
/// "bu kaydi kim degistirdi" sorusu cevapsiz kalirdi. Tek noktada,
/// kaydetme aninda yapiliyor.
/// </remarks>
public sealed class AuditableEntityInterceptor(
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditFields(DbContext? context)
    {
        if (context is null)
            return;

        var now = dateTimeProvider.UtcNow;
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                // Silme istegi, silinebilir olmayan varliklarda isaretlemeye cevrilir.
                // Bu donusum burada yapilmazsa her handler'in "Remove yerine
                // IsDeleted = true yaz" kuralini hatirlamasi gerekirdi; biri
                // unuttugunda gecmis satis kayitlarinin bagli oldugu satir silinirdi.
                case EntityState.Deleted when entry.Entity is ISoftDeletable softDeletable:
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;
                    softDeletable.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                default:
                    break;
            }
        }
    }
}
