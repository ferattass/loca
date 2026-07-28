using FluentValidation;
using MediatR;

namespace Loca.Application.Common.Behaviors;

/// <summary>
/// Handler calismadan once ilgili tum dogrulayicilari calistirir.
/// </summary>
/// <remarks>
/// Dogrulama her handler'in basinda tekrar tekrar yazilirsa bir gun biri
/// unutulur. Pipeline'a tek sefer takilinca kural sudur: bir istegin
/// <c>IValidator</c>'u varsa handler'a ancak gecerli veriyle ulasilir.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Dogrulayicisi olmayan istekler (cogu sorgu) bosuna maliyet olusturmasin.
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        // Ilk hatada durulmaz; TUM hatalar tek cevapta doner ki kullanici
        // formu deneme yanilma ile duzeltmek zorunda kalmasin.
        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
