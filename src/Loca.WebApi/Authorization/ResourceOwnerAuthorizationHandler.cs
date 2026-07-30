using System.Security.Claims;
using Loca.Application.Common.Authentication;
using Loca.Application.Common.Authorization;
using Loca.Domain.Common;
using Loca.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Loca.WebApi.Authorization;

/// <summary>Kaynak sahipligi sarti.</summary>
public sealed class ResourceOwnerRequirement : IAuthorizationRequirement;

/// <summary>
/// Ucuncu seviye yetkilendirme: istegi yapan, kaynagin sahibi mi?
/// </summary>
/// <remarks>
/// Bu seviye atlandiginda ortaya cikan acik klasiktir: organizator rolundeki
/// A kullanicisi, ayni role sahip oldugu icin B'nin etkinligini duzenleyebilir.
/// Kabul olcutu net — A'nin token'iyla B'nin kaynagina istek <b>403</b> donmeli;
/// 200 de 404 de degil.
/// </remarks>
internal sealed class ResourceOwnerAuthorizationHandler
    : AuthorizationHandler<ResourceOwnerRequirement, IOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        IOwnedResource resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        var subject = context.User.FindFirstValue(ClaimNames.Subject);
        Guid? userId = Guid.TryParse(subject, out var parsed) ? parsed : null;

        // Kuralin kendisi Application katmaninda: ayni karar handler'lardan
        // da veriliyor ve iki yerde farkli davranmamasi gerekiyor.
        if (Ownership.Allows(userId, context.User.IsInRole(RoleNames.Admin), resource))
            context.Succeed(requirement);

        // Basarisizlikta Fail() cagrilmaz: baska bir handler ayni sarti
        // karsilayabilir. Hicbiri basarili olmazsa istek zaten reddedilir.
        return Task.CompletedTask;
    }
}
