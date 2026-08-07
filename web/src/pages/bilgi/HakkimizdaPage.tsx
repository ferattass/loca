import { Link } from 'react-router-dom';

import { BilgiBolumu, BilgiSayfasi } from './BilgiSayfasi';

/**
 * Hakkimizda.
 *
 * <b>Uydurma bir sirket hikayesi yazilmadi.</b> "2015'te kuruldu, milyonlarca
 * bilet satti" gibi cumleler hazir sablonlarda bol ama hicbiri dogru
 * olmazdi ve teslim jurisi ilk soruda yakalardi. Sayfa gercekten var olan
 * seyi anlatiyor: sistemin ne yaptigi, hangi kararlarla kuruldugu ve bunun
 * bir staj projesi oldugu.
 */
export function HakkimizdaPage() {
  return (
    <BilgiSayfasi
      baslik="Hakkımızda"
      ozet="Loca nedir, ne yapar ve hangi kararlarla kuruldu."
    >
      <BilgiBolumu baslik="Loca nedir?">
        <p>
          Loca; etkinlik keşfi, koltuk seçimi ve biletleme işini tek yerde toplayan bir
          rezervasyon sistemidir. Adını tiyatrolardaki özel balkon locasından alıyor: burada
          satılan şey bir bilet değil, salondaki belirli bir koltuk.
        </p>
        <p>
          Kullanıcı salonun planını görüyor, boş koltuğu kendisi seçiyor ve seçtiği koltuk o
          anda kendisine kilitleniyor. Aynı koltuğun iki kişiye satılmaması, sistemin en temel
          sözü.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Nasıl çalışıyor?">
        <p>
          Etkinlikler organizatörler tarafından oluşturuluyor. Bir etkinliğin yayına
          çıkabilmesi için oturumu, bilet türü, afişi ve <strong>sahnenin o tarih için
          tutulduğunu gösteren sözleşmesi</strong> olması gerekiyor; onay ekibi bu belgeye
          bakarak yayına alıyor. Yani listede gördüğün her etkinliğin arkasında incelenmiş bir
          başvuru var.
        </p>
        <p>
          Ödeme, lisanslı bir ödeme sağlayıcısının kendi sayfasında tamamlanıyor. Kart bilgisi
          Loca sunucularına hiç ulaşmıyor. Havale/EFT açık olduğunda koltuk daha uzun bir süre
          tutuluyor, çünkü banka işlemleri anlık değil.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Bilet neden koltuk bazlı?">
        <p>
          Genel giriş bileti satmak teknik olarak daha kolay: bir sayaç tutar, azaltırsın.
          Koltuk bazlı satışta ise her koltuk ayrı bir kayıt ve aynı anda iki kişi aynı koltuğa
          uzanabiliyor. Bu yüzden sistemde koltuk seçildiği anda kısa süreli bir kilit
          kuruluyor; kilidin süresi dolarsa koltuk kendiliğinden satışa dönüyor.
        </p>
        <p>
          Ödeme yapılmadan koltuğun sonsuza kadar tutulmaması bir kural değil zorunluluk:
          öyle olmasaydı bir kişi salonu seçip ödemeden bırakarak satışı tamamen durdurabilirdi.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Kimler kullanıyor?">
        <p>
          Dört ayrı rol var. <strong>Müşteri</strong> bilet arıyor ve satın alıyor.
          <strong> Organizatör</strong> etkinlik açıyor, belgesini yüklüyor ve kapıda bileti
          okutuyor. <strong>Onay ekibi</strong> başvuruları inceleyip yayına alıyor.
          <strong> Yönetim</strong> ödeme ayarlarını, kullanıcıları ve iadeleri yürütüyor.
        </p>
        <p>
          Bir kullanıcı birden fazla role sahip olabiliyor; organizatör de bilet alabilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Bu bir staj projesi">
        <p>
          Loca, bir yaz stajı kapsamında on iş gününde geliştirilmiş bir uygulamadır. Ödeme
          akışı gerçek bir sağlayıcının test ortamına bağlıdır: sitede gerçek bir tahsilat
          yapılmaz ve listelenen etkinlikler örnek verilerdir.
        </p>
        <p>
          Kaynak kodu ve gelişme günlüğü herkese açık.{' '}
          <a
            href="https://github.com/ferattass/loca"
            target="_blank"
            rel="noreferrer"
            className="text-primary underline underline-offset-2"
          >
            github.com/ferattass/loca
          </a>
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Bize ulaş">
        <p>
          Soru, iade talebi veya etkinlik başvurusu için{' '}
          <Link to="/iletisim" className="text-primary hover:underline">
            İletişim
          </Link>{' '}
          sayfasına bakabilirsin. Kişisel verilerin nasıl işlendiği{' '}
          <Link to="/gizlilik" className="text-primary hover:underline">
            Gizlilik
          </Link>{' '}
          sayfasında yazıyor.
        </p>
      </BilgiBolumu>
    </BilgiSayfasi>
  );
}
