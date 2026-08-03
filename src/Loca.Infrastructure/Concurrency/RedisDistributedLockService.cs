using Loca.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Loca.Infrastructure.Concurrency;

/// <summary>
/// Redis uzerinde TTL'li dagitik kilit.
/// </summary>
/// <remarks>
/// Alma: <c>SET key token NX PX ttl</c> — tek komut, atomik. Once <c>GET</c>
/// ile bakip sonra <c>SET</c> yapmak iki komut arasinda baskasinin araya
/// girmesine acik olurdu.
///
/// <para>
/// Birakma: Lua betigi ile. Neden dogrudan <c>DEL</c> degil — kilit TTL
/// dolarak dusmus ve ayni anahtari BASKASI almis olabilir. Duz <c>DEL</c> o
/// kisinin kilidini silerdi ve iki kullanici ayni koltugu ayni anda tutar
/// hâle gelirdi. Betik once degeri kendi token'iyla karsilastiriyor;
/// karsilastirma ve silme Redis tarafinda tek adimda calisiyor.
/// </para>
/// </remarks>
internal sealed class RedisDistributedLockService(
    RedisConnection connection,
    ILogger<RedisDistributedLockService> logger) : IDistributedLockService
{
    /// <summary>
    /// Yalnizca kendi token'ini birakan betik.
    /// </summary>
    /// <remarks>
    /// Karsilastir-ve-sil tek adimda calismali; Redis betigi tek is parcacigi
    /// uzerinde bolunmeden calistirir.
    /// </remarks>
    private const string ReleaseScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
          return redis.call("del", KEYS[1])
        else
          return 0
        end
        """;

    public async Task<IDistributedLock?> AcquireAsync(
        IReadOnlyCollection<string> keys,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // SIRALAMA ONEMLI. Iki istek {A,B} ve {B,A} sirasiyla isteseydi her
        // biri digerinin bekledigi anahtari tutar ve ikisi de basarisiz olurdu
        // — koltuk kimseye gitmezdi. Sabit siralamada biri once davranir ve
        // digeri temiz bir "dolu" cevabi alir.
        var sirali = keys
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (sirali.Count == 0)
            return new NoOpLock();

        // Her kilit denemesi kendi token'ini uretir; birakirken bu token
        // dogrulanacak.
        var token = Guid.NewGuid().ToString("N");
        var alinanlar = new List<string>(sirali.Count);

        var database = connection.GetDatabase();

        if (database is null)
            return Degraded(exception: null);

        foreach (var key in sirali)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool alindi;

            try
            {
                alindi = await database.StringSetAsync(key, token, ttl, When.NotExists);
            }
            catch (Exception exception) when (
                exception is RedisConnectionException or RedisTimeoutException or RedisServerException)
            {
                // Sartname: Redis kapaliyken sistem cokmemeli. Alinmis
                // kilitler birakilmaya calisilir (o cagri da basarisiz
                // olabilir; TTL zaten temizleyecek) ve akis veritabani
                // savunmasiyla devam eder.
                await TryReleaseAsync(database, alinanlar, token);
                return Degraded(exception);
            }

            if (!alindi)
            {
                // Koltuk baskasinda. Bu ana kadar alinanlar hemen birakilir:
                // TTL dolana kadar beklenseydi kullanicinin istemedigi
                // koltuklar da on dakika bloke kalirdi.
                await TryReleaseAsync(database, alinanlar, token);

                logger.LogInformation(
                    "Koltuk kilidi alinamadi, baskasinda. Anahtar: {Anahtar}", key);

                return null;
            }

            alinanlar.Add(key);
        }

        return new RedisLock(database, alinanlar, token, logger);
    }

    // Donus tipi arayuz degil somut sinif: metot private ve tek cagrilan yer
    // zaten arayuze yukseltiyor (CA1859).
    private NoOpLock Degraded(Exception? exception)
    {
        // Uyari seviyesinde: hata degil ama sessiz gecilmemeli. Bu satiri
        // gormeden "kilit calisiyor" varsayilirsa yarisan iki istegin neden
        // veritabanina kadar gittigi anlasilmaz.
        logger.LogWarning(
            exception,
            "Redis'e ulasilamadi; koltuk kilidi kurulamadi. " +
            "Akis yalnizca veritabani transaction'i ve eszamanlilik damgasiyla devam ediyor.");

        return new NoOpLock { IsDegraded = true };
    }

    private static async Task TryReleaseAsync(IDatabase database, List<string> keys, string token)
    {
        foreach (var key in keys)
        {
            try
            {
                await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
            }
            catch (Exception exception) when (
                exception is RedisConnectionException or RedisTimeoutException or RedisServerException)
            {
                // Yutuluyor: kilit birakilamadiysa TTL en fazla kilit suresi
                // kadar sonra kendiliginden dusurur. Burada firlatmak, asil
                // isi (rezervasyon) basarili olmus bir istegi hataya
                // cevirirdi.
            }
        }
    }

    /// <summary>Gercekten Redis'te tutulan kilit.</summary>
    private sealed class RedisLock(
        IDatabase database, List<string> keys, string token, ILogger logger) : IDistributedLock
    {
        public bool IsDegraded => false;

        public async ValueTask DisposeAsync()
        {
            foreach (var key in keys)
            {
                try
                {
                    await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
                }
                catch (Exception exception) when (
                    exception is RedisConnectionException or RedisTimeoutException or RedisServerException)
                {
                    logger.LogWarning(
                        exception,
                        "Kilit birakilamadi, TTL ile dusecek. Anahtar: {Anahtar}", key);
                }
            }
        }
    }

    /// <summary>
    /// Birakilacak bir sey olmayan tutamak: bos anahtar listesi veya
    /// Redis'siz calisma.
    /// </summary>
    private sealed class NoOpLock : IDistributedLock
    {
        public bool IsDegraded { get; init; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
