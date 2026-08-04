using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Payments.ResolveProviderCallback;

/// <summary>
/// Saglayicinin dondugu kimlikten kendi odememizi bulur.
/// </summary>
/// <remarks>
/// <b>Burasi odemeyi TAMAMLAMIYOR</b>, yalnizca hangi rezervasyona ait
/// oldugunu soyluyor. Tamamlama, kullanicinin oturumuyla arayuzden
/// cagriliyor.
///
/// <para>
/// Sebep: saglayici geri donusu tarayici uzerinden bir form POST'u olarak
/// geliyor ve bu istekte bizim oturum belirtecimiz yok. Tamamlamayi burada
/// yapmak, kimligi dogrulanmamis bir istegin odeme kapatmasi demek olurdu.
/// Kapatma yetkisi icin oturum kontrolu gevsetilseydi, token'i ele geciren
/// biri baskasinin odemesini kapatabilirdi.
/// </para>
///
/// <para>
/// Kullanici odemeden sonra tarayiciyi kapatirsa tamamlama hic
/// cagrilmayabilir. Bu durumda odeme <c>Pending</c> kalir ve rezervasyon
/// suresi dolunca koltuklar serbest birakilir; para saglayicida bekler ve
/// mutabakatta gorulur. Kalici cozum, bekleyen odemeleri saglayiciya
/// soran bir zamanlanmis is — Gun 8'e birakildi.
/// </para>
/// </remarks>
public sealed record ResolveProviderCallbackQuery(string ProviderReference)
    : IRequest<Result<ProviderCallbackTarget>>;

internal sealed class ResolveProviderCallbackQueryHandler(IPaymentRepository payments)
    : IRequestHandler<ResolveProviderCallbackQuery, Result<ProviderCallbackTarget>>
{
    public async Task<Result<ProviderCallbackTarget>> Handle(
        ResolveProviderCallbackQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderReference))
            return Result.Failure<ProviderCallbackTarget>(PaymentErrors.NotFound);

        var odeme = await payments.GetByProviderReferenceAsync(
            request.ProviderReference, cancellationToken);

        return odeme is null
            ? Result.Failure<ProviderCallbackTarget>(PaymentErrors.NotFound)
            : Result.Success(new ProviderCallbackTarget(odeme.Id, odeme.ReservationId));
    }
}
