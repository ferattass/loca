using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Common;
using Loca.Domain.Constants;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.Admin.ChangeUserRole;

/// <param name="Grant">
/// <c>true</c> rolu verir, <c>false</c> geri alir.
/// </param>
/// <remarks>
/// Verme ve alma tek komutta: ikisi de ayni kurallara tabi (rol var mi,
/// kullanici var mi, kendi admin rolunu alamaz) ve ayri komut olsaydi bu
/// kurallar iki yerde tekrar edilirdi.
/// </remarks>
public sealed record ChangeUserRoleCommand(Guid UserId, string RoleName, bool Grant)
    : IRequest<Result>;

internal sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(komut => komut.UserId).NotEmpty();

        // Rol adi serbest metin degil: sistemdeki uc rolden biri olmali.
        // Aksi halde yazim hatasi olan bir rol veritabanina yazilir ve
        // hicbir yetki kontrolune takilmadigi icin sessizce etkisiz kalirdi.
        RuleFor(komut => komut.RoleName)
            .NotEmpty()
            .Must(ad => RoleNames.All.Contains(ad, StringComparer.Ordinal))
            .WithMessage($"Rol su degerlerden biri olmali: {string.Join(", ", RoleNames.All)}");
    }
}

internal sealed class ChangeUserRoleCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<ChangeUserRoleCommandHandler> logger)
    : IRequestHandler<ChangeUserRoleCommand, Result>
{
    public async Task<Result> Handle(
        ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kendi admin rolunu alma denemesi EN BASTA reddediliyor. Tek adminli
        // bir sistemde panele girisi olan hic kimse kalmayabilirdi ve geri
        // donusu yalnizca veritabanina elle mudahaleyle mumkun olurdu.
        if (!request.Grant &&
            request.RoleName == RoleNames.Admin &&
            currentUser.UserId == request.UserId)
        {
            return Result.Failure(AdminErrors.CannotRemoveOwnAdminRole);
        }

        var kullanici = await users.GetByIdAsync(request.UserId, cancellationToken);

        if (kullanici is null)
            return Result.Failure(AdminErrors.UserNotFound);

        var rol = await users.GetRoleByNameAsync(request.RoleName, cancellationToken);

        if (rol is null)
            return Result.Failure(AdminErrors.RoleNotFound);

        if (request.Grant)
            kullanici.AssignRole(rol);
        else
            kullanici.RemoveRole(rol);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Rol degisikligi denetim kaydina deger: yetki artiran bir islem
        // ve sonradan "bunu kim yapti" sorusu mutlaka soruluyor.
        logger.LogInformation(
            "Rol {Islem}. KullaniciId: {KullaniciId}, Rol: {Rol}, YapanId: {YapanId}",
            request.Grant ? "verildi" : "alindi",
            request.UserId,
            request.RoleName,
            currentUser.UserId);

        return Result.Success();
    }
}
