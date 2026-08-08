using System.Text;
using Loca.Application.Common.Authentication;
using Loca.Domain.Constants;
using Loca.Infrastructure.Authentication;
using Loca.WebApi.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Loca.WebApi.Extensions;

/// <summary>
/// Kimlik dogrulama ve yetkilendirmenin kurulumu.
/// </summary>
public static class KimlikKurulumu
{
    /// <summary>
    /// JWT bearer dogrulamasini kaydeder.
    /// </summary>
    public static IServiceCollection KimlikDogrulamaEkle(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt yapilandirmasi bulunamadi.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Claim adlari oldugu gibi okunur. Varsayilan esleme "sub" claim'ini
                // uzun bir URI'ye cevirir ve token'i ureten tarafla okuyan taraf
                // birbirini bulamaz.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),

                    ValidateLifetime = true,

                    // Varsayilan tolerans 5 dakika. 15 dakikalik bir access token'da
                    // bu, omrun ucte birini uzatmak demek — sifira cekildi.
                    ClockSkew = TimeSpan.Zero,

                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role
                };
            });

        return services;
    }

    /// <summary>
    /// Rol politikalarini ve kaynak sahipligi kuralini kaydeder.
    /// </summary>
    public static IServiceCollection YetkilendirmeEkle(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(Policies.OrganizerOnly, policy =>
                policy.RequireRole(RoleNames.Organizer, RoleNames.Admin))
            // Admin de dahil: onay yetkisi admin'in zaten sahip oldugu yetkilerin
            // alt kumesi ve haric tutulsaydi tek admin hesabiyla kurulan bir
            // sistemde onay kuyrugu hic acilamazdi.
            .AddPolicy(Policies.ModeratorOnly, policy =>
                policy.RequireRole(RoleNames.Moderator, RoleNames.Admin))
            .AddPolicy(Policies.ResourceOwner, policy =>
                policy.AddRequirements(new ResourceOwnerRequirement()));

        services.AddSingleton<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();

        return services;
    }
}
