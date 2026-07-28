using FluentValidation;
using Loca.Application.Common.Behaviors;
using Loca.Application.Features.Auth.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Loca.Application;

/// <summary>
/// Uygulama katmaninin DI kayitlari. Program.cs'in katmanlarin ic yapisini
/// bilmesi gerekmesin diye her katman kendi kaydini kendi yapar.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);

            // Sira onemli: once eklenen disda kalir. Loglama en disarida durur ki
            // dogrulamada reddedilen istekler de olculsun ve kayda gecsin.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Validator'lar elle kaydedilmez; yeni bir validator yazildiginda
        // kendiliginden devreye girsin diye assembly taranir.
        services.AddValidatorsFromAssembly(assembly);

        // Ozellik ici yardimci servisler. Assembly taramasina girmedikleri icin
        // acikca kaydedilir.
        services.AddScoped<AuthTokenIssuer>();

        return services;
    }
}
