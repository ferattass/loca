namespace Loca.Domain.Common;

/// <summary>
/// Platformun bilet fiyatinin ustune ekledigi hizmet bedeli.
/// </summary>
/// <remarks>
/// <b>Neden ustune ekleniyor, komisyon olarak kesilmiyor:</b> organizator
/// koltuga bir fiyat yaziyor ve o fiyati almayi bekliyor. Komisyon
/// kesilseydi platformun payi organizatorun cebinden cikardi ve hakedis
/// defteri, odeme donemi, mutabakat gibi bir yigin yapi gerekirdi. Hizmet
/// bedeli musteriden aliniyor, sepette ayri satir olarak gorunuyor ve
/// organizatorun aldigi tutar degismiyor.
///
/// <para>
/// <b>Alt sinir bilet BASINA.</b> Yalnizca yuzde olsaydi bes liralik bir
/// ogrenci biletinden kirk kurus alinirdi; odeme saglayicisinin islem
/// basina aldigi ucret bile bunun ustunde ve platform her ucuz bilette
/// zarar ederdi. Yalnizca sabit ucret olsaydi bin liralik locadan da bes
/// lira alinirdi. Ikisinin BUYUGU aliniyor.
/// </para>
///
/// <para>
/// Deger nesnesi ve saf mantik: veritabanina, saate ve HTTP'ye dokunmadan
/// test edilebiliyor.
/// </para>
/// </remarks>
public readonly record struct ServiceFeePolicy
{
    public ServiceFeePolicy(decimal percent, decimal minimumPerTicket)
    {
        if (percent < 0)
            throw new DomainException("Hizmet bedeli yuzdesi negatif olamaz.");

        // Yuzde yuzden buyuk bir hizmet bedeli, bileti ikiye katlamaktan
        // fazlasi demek; yazim hatasi (8 yerine 80) burada yakalaniyor.
        if (percent > 100)
            throw new DomainException("Hizmet bedeli yuzdesi 100'u asamaz.");

        if (minimumPerTicket < 0)
            throw new DomainException("Bilet basina alt sinir negatif olamaz.");

        Percent = percent;
        MinimumPerTicket = minimumPerTicket;
    }

    /// <summary>Kalemler toplamina uygulanan yuzde. 8 = %8.</summary>
    public decimal Percent { get; }

    /// <summary>Bilet basina en az alinacak tutar.</summary>
    public decimal MinimumPerTicket { get; }

    /// <summary>Hicbir bedel almayan politika.</summary>
    /// <remarks>
    /// Ayarlar tanimsizken bu kullaniliyor: varsayilan olarak para
    /// EKLENMIYOR. Ters olsaydi, ayari hic gormemis bir kurulum
    /// musteriden habersizce ucret alirdi.
    /// </remarks>
    public static ServiceFeePolicy None => new(0, 0);

    /// <summary>Hicbir sey eklemiyor mu.</summary>
    public bool IsZero => Percent == 0 && MinimumPerTicket == 0;

    /// <param name="itemsTotal">Koltuk fiyatlarinin toplami.</param>
    /// <param name="ticketCount">Bilet (koltuk) adedi.</param>
    /// <returns>Toplama eklenecek hizmet bedeli.</returns>
    public Money Calculate(Money itemsTotal, int ticketCount)
    {
        if (ticketCount <= 0)
            throw new DomainException("Bilet adedi sifirdan buyuk olmali.");

        if (IsZero)
            return Money.Zero(itemsTotal.Currency);

        var yuzdeden = itemsTotal.Amount * Percent / 100m;
        var altSinirdan = MinimumPerTicket * ticketCount;

        // Money yapicisi bankaci yuvarlamasi yapiyor; burada yuvarlanmamis
        // degerler karsilastiriliyor ki 4,999 ile 5,001 arasindaki secim
        // yuvarlama sonrasi degil oncesi yapilsin.
        return new Money(Math.Max(yuzdeden, altSinirdan), itemsTotal.Currency);
    }
}
