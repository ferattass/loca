using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Loca.Infrastructure.Concurrency;

/// <summary>
/// Redis baglantisini <b>tembel</b> kurar ve basarisizligi yutar.
/// </summary>
/// <remarks>
/// Sartname Redis kapaliyken sistemin cokmemesini istiyor. Bu iki ayri anda
/// gecerli olmali:
/// <list type="number">
/// <item>
/// <b>Acilista.</b> Baglanti <c>Program.cs</c>'te kurulsaydi ve Redis kapali
/// olsaydi uygulama hic ayaga kalkmazdi — yani kilit servisi olmadan
/// calisabilen bir sistem, kilit servisi yuzunden tamamen durmus olurdu.
/// Baglanti ilk kullanimda kuruluyor.
/// </item>
/// <item>
/// <b>Calisma aninda.</b> <c>AbortOnConnectFail = false</c> ile kutuphane
/// baglanamadiginda istisna firlatmak yerine arka planda yeniden deniyor;
/// Redis geri geldiginde kilitler kendiliginden calismaya baslar.
/// </item>
/// </list>
///
/// <para>
/// <c>Lazy</c> istisnayi da onbellekler ama burada sorun degil: hatali
/// baglanti dizesi (bicim hatasi) zaten yeniden denemekle duzelmez, ayakta
/// olmayan sunucu ise istisna firlatmiyor.
/// </para>
/// </remarks>
internal sealed class RedisConnection : IDisposable
{
    private readonly Lazy<IConnectionMultiplexer?> _multiplexer;

    public RedisConnection(string connectionString, ILogger<RedisConnection> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        _multiplexer = new Lazy<IConnectionMultiplexer?>(
            () =>
            {
                try
                {
                    var yapilandirma = ConfigurationOptions.Parse(connectionString);

                    yapilandirma.AbortOnConnectFail = false;

                    // Varsayilan 5 saniye. Rezervasyon akisinda kilit
                    // ON eleme adimi; ayakta olmayan bir Redis icin bes
                    // saniye beklemek kullaniciya sistemin kilitlendigini
                    // dusundururdu.
                    yapilandirma.ConnectTimeout = 2000;
                    yapilandirma.SyncTimeout = 2000;

                    return ConnectionMultiplexer.Connect(yapilandirma);
                }
                catch (Exception exception) when (
                    exception is RedisConnectionException or ArgumentException or FormatException)
                {
                    logger.LogWarning(
                        exception,
                        "Redis baglantisi kurulamadi. Koltuk kilidi devre disi, " +
                        "eszamanlilik yalnizca veritabani tarafinda korunuyor.");

                    return null;
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Kullanilabilir veritabani; Redis yoksa <c>null</c>.</summary>
    public IDatabase? GetDatabase()
    {
        try
        {
            return _multiplexer.Value?.GetDatabase();
        }
        catch (RedisConnectionException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_multiplexer.IsValueCreated)
            _multiplexer.Value?.Dispose();
    }
}
