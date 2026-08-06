using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using MediatR;

namespace Loca.Application.Features.Admin.GetUserDetail;

/// <summary>
/// Tek bir kullanicinin panelde gorunen tum bilgisi.
/// </summary>
/// <remarks>
/// <b>Sifre ozeti ve oturum belirteci HIC DONMEZ.</b> Panelde gosterecek
/// bir yeri yok ve donselerdi tarayici gecmisine, ekran goruntusune ve
/// vekil sunucu kayitlarina duserdi.
///
/// <para>
/// Bilet QR kodlari da donmuyor: yonetici bir kullanicinin biletlerini
/// gormeli ama o biletlerle kapidan gecebilmemeli.
/// </para>
/// </remarks>
public sealed record GetUserDetailQuery(Guid Id) : IRequest<Result<AdminKullaniciDetayi>>;

internal sealed class GetUserDetailQueryHandler(IAdminQueries queries)
    : IRequestHandler<GetUserDetailQuery, Result<AdminKullaniciDetayi>>
{
    public async Task<Result<AdminKullaniciDetayi>> Handle(
        GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detay = await queries.GetUserDetailAsync(request.Id, cancellationToken);

        return detay is null
            ? Result.Failure<AdminKullaniciDetayi>(AdminErrors.UserNotFound)
            : Result.Success(detay);
    }
}
