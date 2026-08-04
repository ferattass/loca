using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.GetQueue;

/// <param name="Durum">
/// <c>Pending</c>, <c>Retryable</c>, <c>DeadLettered</c> veya
/// <c>Processed</c>. Taninmayan deger <c>Pending</c> sayilir.
/// </param>
public sealed record GetQueueQuery(string Durum, int Limit = 50)
    : IRequest<Result<IReadOnlyList<KuyrukMesaji>>>;

internal sealed class GetQueueQueryValidator : AbstractValidator<GetQueueQuery>
{
    public GetQueueQueryValidator()
    {
        // Ust sinir: kuyruk bir milyon satira ulastiginda "hepsini goster"
        // diyen bir istek sunucuyu da tarayiciyi da kilitlerdi.
        RuleFor(sorgu => sorgu.Limit).InclusiveBetween(1, 200);
    }
}

internal sealed class GetQueueQueryHandler(IAdminQueries queries)
    : IRequestHandler<GetQueueQuery, Result<IReadOnlyList<KuyrukMesaji>>>
{
    public async Task<Result<IReadOnlyList<KuyrukMesaji>>> Handle(
        GetQueueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mesajlar = await queries.GetQueueAsync(
            request.Durum, request.Limit, cancellationToken);

        return Result.Success(mesajlar);
    }
}

/// <summary>Olu mektubu kuyruga geri koyar.</summary>
/// <remarks>
/// <b>Elle tetiklenir.</b> Deneme hakki tukenmis mesaji is akisi bir daha
/// ele almiyor; sebep giderildikten sonra (orn. e-posta sunucusu duzeldi)
/// kuyruga geri koymanin bir yolu olmali. Bu uc olmasaydi tek care
/// veritabanina dogrudan UPDATE atmak olurdu ve o islem hicbir yerde
/// kayitli kalmazdi.
/// </remarks>
public sealed record RequeueMessageCommand(Guid MessageId) : IRequest<Result>;

internal sealed class RequeueMessageCommandHandler(
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<RequeueMessageCommandHandler> logger)
    : IRequestHandler<RequeueMessageCommand, Result>
{
    public async Task<Result> Handle(
        RequeueMessageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mesaj = await outbox.GetByIdAsync(request.MessageId, cancellationToken);

        if (mesaj is null)
            return Result.Failure(AdminErrors.QueueMessageNotFound);

        if (!mesaj.IsDeadLettered)
            return Result.Failure(AdminErrors.QueueMessageNotDeadLettered);

        mesaj.RequeueFromDeadLetter();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Kim geri koydu bilgisi loga yaziliyor: kuyruga elle mudahale
        // izlenebilir olmali.
        logger.LogInformation(
            "Olu mektup kuyruga geri konuldu. MesajId: {MesajId}, Tur: {Tur}, KullaniciId: {KullaniciId}",
            mesaj.Id,
            mesaj.Type,
            currentUser.UserId);

        return Result.Success();
    }
}
