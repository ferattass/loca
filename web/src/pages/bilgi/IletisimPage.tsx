import { Link } from 'react-router-dom';

import { BilgiBolumu, BilgiSayfasi } from './BilgiSayfasi';

const DESTEK_ADRESI = 'destek@loca.dev';

/**
 * Iletisim.
 *
 * <b>Form yok, bilincli.</b> Calisan bir iletisim formu icin mesaji saklayan
 * bir tablo, hiz siniri ve yoneticinin okuyacagi bir ekran gerekiyor;
 * bunlarin hicbiri yazilmadan konan form, gonder'e basildiginda hicbir sey
 * yapmayan bir dugme olurdu. Alt bilgideki uc baglantinin duz metin olarak
 * durmasinin sebebi de zaten buydu — calismayan bir sey eklemek yerine
 * calisan bir sey konuldu: dogrudan acilan posta adresi.
 */
export function IletisimPage() {
  return (
    <BilgiSayfasi
      baslik="İletişim"
      ozet="Bilet, iade ve hesap konularında bize nasıl ulaşabileceğin."
    >
      <BilgiBolumu baslik="Destek">
        <p>
          Bilet, rezervasyon ve iade konularındaki taleplerini{' '}
          <a
            href={`mailto:${DESTEK_ADRESI}`}
            className="font-semibold text-primary hover:underline"
          >
            {DESTEK_ADRESI}
          </a>{' '}
          adresine yazabilirsin. Mesajlar hafta içi 09.00–18.00 arasında yanıtlanıyor.
        </p>
        <p>
          Yazarken <strong>rezervasyon numaranı</strong> ve kayıtlı e-posta adresini
          eklersen işlem tek yazışmada tamamlanıyor. Rezervasyon numarasını{' '}
          <Link to="/rezervasyonlarim" className="text-primary hover:underline">
            Rezervasyonlarım
          </Link>{' '}
          sayfasında bulabilirsin.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Etkinlik düzenlemek istiyorum">
        <p>
          Kendi etkinliğini yayınlamak için organizatör yetkisi gerekiyor. Aynı adrese
          etkinlik türünü, tahmini seyirci sayısını ve çalışmak istediğin mekânı yazarak
          başvurabilirsin; başvuru yönetim tarafından değerlendirilip hesabına organizatör
          rolü tanımlanıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Kişisel verilerinle ilgili talepler">
        <p>
          Hesabındaki verilerin silinmesini veya bir kopyasını istiyorsan aynı adrese
          yazman yeterli. Hangi verilerin ne kadar süre tutulduğu{' '}
          <Link to="/kullanim-kosullari" className="text-primary hover:underline">
            Kullanım koşulları
          </Link>{' '}
          sayfasında yazıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Bu bir staj projesi">
        <p>
          Loca, bir yaz stajı kapsamında geliştirilen bir uygulamadır. Ödeme akışı gerçek
          bir sağlayıcının test ortamına bağlıdır; sitede gerçek bir tahsilat yapılmaz ve
          listelenen etkinlikler örnek verilerdir.
        </p>
      </BilgiBolumu>
    </BilgiSayfasi>
  );
}
