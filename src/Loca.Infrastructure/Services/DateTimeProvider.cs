using Loca.Application.Common.Interfaces;

namespace Loca.Infrastructure.Services;

/// <summary>
/// Gercek sistem saati. Testlerde bunun yerine sabit saat veren
/// bir sahte uygulama kullanilir.
/// </summary>
internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
