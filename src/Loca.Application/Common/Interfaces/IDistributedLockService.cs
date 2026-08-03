namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Surecler arasi kilit. Katmanli savunmanin BIRINCI halkasi.
/// </summary>
/// <remarks>
/// <b>Bu kilit dogruluk icin degil, hiz ve kullanici deneyimi icindir.</b>
/// Ayni koltugu isteyen ikinci kullaniciya veritabanina hic gitmeden hizli
/// bir "dolu" cevabi doner. Gercek tutarlilik garantisi veritabani
/// transaction'i ve <c>xmin</c> eszamanlilik damgasindadir.
///
/// <para>
/// <b>Kilidi almis olmak koltugun veritabaninda bos oldugunu GARANTI ETMEZ:</b>
/// TTL dolmus, Redis yeniden baslamis veya kilit hic kurulamamis olabilir.
/// Kilit alindiktan sonra durum veritabaninda yeniden dogrulanir.
/// </para>
/// </remarks>
public interface IDistributedLockService
{
    /// <summary>
    /// Verilen anahtarlarin tamamini kilitler; biri bile alinamazsa
    /// alinanlar geri birakilir.
    /// </summary>
    /// <remarks>
    /// Ya hep ya hic: kismi kilit, kullaniciya "uc koltugun ikisi sizin"
    /// demek olurdu ve kalan koltuk kimsede olmadan bloke kalirdi.
    /// </remarks>
    /// <returns>
    /// Kilit tutamagi; anahtarlardan biri baskasindaysa <c>null</c>.
    /// Redis'e ulasilamadiginda <c>null</c> DEGIL, <see cref="IDistributedLock.IsDegraded"/>
    /// isaretli bir tutamak doner — sartname Redis kapaliyken sistemin
    /// calismaya devam etmesini istiyor.
    /// </returns>
    Task<IDistributedLock?> AcquireAsync(
        IReadOnlyCollection<string> keys,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Alinmis kilidin omru. <c>await using</c> ile birakilir.
/// </summary>
/// <remarks>
/// <see cref="IAsyncDisposable"/> secilmesinin sebebi: kilidi elle birakmak
/// unutulabilecek bir adim. Hata yolunda birakilmayan bir kilit, TTL dolana
/// kadar koltugu kimseye vermez.
/// </remarks>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// Redis'e ulasilamadi; kilit gercekte kurulmadi.
    /// </summary>
    /// <remarks>
    /// Akis durmaz ama savunma bir halka eksilir: yalnizca veritabani
    /// transaction'i ve eszamanlilik damgasi kalir. Bu durum loglanir,
    /// cunku sessizce devam etmek "kilit calisiyor" yanilgisi yaratir.
    /// </remarks>
    bool IsDegraded { get; }
}
