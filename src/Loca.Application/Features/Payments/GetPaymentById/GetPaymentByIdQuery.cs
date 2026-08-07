using Loca.Application.Common.Authorization;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Payments.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Payments.GetPaymentById;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<Result<PaymentDetail>>;

internal sealed class GetPaymentByIdQueryHandler(
    IPaymentRepository payments,
    ICurrentUserService currentUser)
    : IRequestHandler<GetPaymentByIdQuery, Result<PaymentDetail>>
{
    public async Task<Result<PaymentDetail>> Handle(
        GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is null)
            return Result.Failure<PaymentDetail>(PaymentErrors.Unauthenticated);

        var odeme = await payments.GetAggregateAsync(request.Id, cancellationToken);

        if (odeme is null)
            return Result.Failure<PaymentDetail>(PaymentErrors.NotFound);

        if (!Ownership.Allows(currentUser.UserId, currentUser.IsInRole(RoleNames.Admin), odeme))
            return Result.Failure<PaymentDetail>(PaymentErrors.NotOwner);

        return Result.Success(new PaymentDetail(
            odeme.Id,
            odeme.ReservationId,
            odeme.Status,
            odeme.Provider,
            odeme.Method,
            odeme.ProviderReference,
            // Yonlendirme adresi yalnizca odeme baslatilirken uretiliyor ve
            // saklanmiyor: tek kullanimlik ve kisa omurlu bir adres, sonradan
            // donulmesi kullaniciyi suresi gecmis bir sayfaya gotururdu.
            null,
            odeme.Amount.Amount,
            odeme.Amount.Currency,
            odeme.CompletedAtUtc,
            odeme.FailureReason,
            odeme.CreatedAt,
            odeme.Transactions
                .OrderBy(islem => islem.OccurredAtUtc)
                .Select(islem => new PaymentAttempt(islem.Type, islem.OccurredAtUtc, islem.Message))
                .ToList()));
    }
}
