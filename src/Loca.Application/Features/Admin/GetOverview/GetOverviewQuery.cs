using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using MediatR;

namespace Loca.Application.Features.Admin.GetOverview;

/// <summary>Yonetim panelinin acilis ekrani.</summary>
/// <remarks>
/// Yetki kontrolu burada DEGIL controller'daki policy'de: bu sorgu yalnizca
/// admin ucundan cagriliyor ve kullaniciya ozel hicbir kisit tasimiyor.
/// Handler'da tekrar kontrol edilseydi ayni kural iki yerde durur, biri
/// degistiginde digeri sessizce eskirdi.
/// </remarks>
public sealed record GetOverviewQuery : IRequest<Result<AdminOzeti>>;

internal sealed class GetOverviewQueryHandler(
    IAdminQueries queries,
    IPaymentService paymentService,
    IDistributedLockService locks,
    IDateTimeProvider clock)
    : IRequestHandler<GetOverviewQuery, Result<AdminOzeti>>
{
    public async Task<Result<AdminOzeti>> Handle(
        GetOverviewQuery request, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;

        // "Bugun" sunucunun UTC gunu. Yerel gune cevrilseydi rapor, sunucu
        // saat diliminin degistigi anda gecmise donuk olarak degisirdi.
        var gunBasi = utcNow.Date;

        var redisAyakta = await locks.IsAvailableAsync(cancellationToken);

        var ozet = await queries.GetOverviewAsync(
            gunBasi, utcNow, paymentService.Name, redisAyakta, cancellationToken);

        return Result.Success(ozet);
    }
}
