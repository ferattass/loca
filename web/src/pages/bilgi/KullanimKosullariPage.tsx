import { Link } from 'react-router-dom';

import { BilgiBolumu, BilgiSayfasi } from './BilgiSayfasi';

/**
 * Kullanim kosullari.
 *
 * Metin hazir bir sablondan alinmadi: her madde sistemin gercekten yaptigi
 * seyi anlatiyor. Ornegin kart bilgisinin hic bu sunucuya ugramamasi ya da
 * QR'in tek kullanimlik olmasi, kod tarafinda boyle oldugu icin yaziyor.
 * Uygulamanin yapmadigi bir seyi taahhut eden bir kosul metni, hukuki
 * olarak da urun olarak da yanlis olurdu.
 */
export function KullanimKosullariPage() {
  return (
    <BilgiSayfasi
      baslik="Kullanım koşulları"
      ozet="Hesap, rezervasyon, ödeme ve kişisel verilerle ilgili kurallar."
    >
      <BilgiBolumu baslik="1. Taraflar ve kapsam">
        <p>
          Bu koşullar, Loca üzerinden etkinlik bileti arayan, rezervasyon açan veya bilet
          satın alan kullanıcı ile platform arasındaki ilişkiyi düzenler. Siteyi
          kullanarak bu koşulları kabul etmiş olursun.
        </p>
        <p>
          Loca bir staj projesidir. Sitede gerçek bir tahsilat yapılmaz; ödeme akışı
          sağlayıcının test ortamına bağlıdır ve listelenen etkinlikler örnek verilerdir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="2. Hesap">
        <p>
          Rezervasyon açmak için hesap gerekiyor. Verdiğin e-posta adresinin sana ait ve
          erişilebilir olması gerekir: bilet bildirimleri ve şifre sıfırlama bağlantısı bu
          adrese gider. Hesabının güvenliğinden sen sorumlusun; şifreni değiştirdiğinde
          açık olan tüm oturumlar güvenlik gereği kapatılır.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="3. Rezervasyon ve koltuk kilidi">
        <p>
          Koltuk seçtiğinde koltuklar sınırlı bir süre için sana kilitlenir. Kilit,
          ödemenin tamamlanması için verilen süredir ve bir kez uzatılabilir. Süre
          dolduğunda rezervasyon kendiliğinden düşer, koltuklar satışa döner ve bu durum
          bir hak kaybı sayılmaz.
        </p>
        <p>
          Bir oturumda tek kullanıcının tutabileceği koltuk sayısı sınırlıdır. Sistemi
          otomatik araçlarla kullanmak, aynı koltukları tekrar tekrar kilitleyerek satışı
          engellemek ve benzeri davranışlar hesabın kapatılmasına yol açabilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="4. Ödeme">
        <p>
          Kartla ödeme, lisanslı bir ödeme sağlayıcısının kendi sayfası üzerinden alınır.{' '}
          <strong>Kart numaran, son kullanma tarihin ve CVV bilgin Loca sunucularına
          hiçbir zaman ulaşmaz</strong>, kaydedilmez ve loglanmaz. Loca yalnızca ödemenin
          sonucunu (başarılı/başarısız ve sağlayıcı referansı) saklar.
        </p>
        <p>
          Havale/EFT açık olduğunda ödeme için ayrı ve daha uzun bir süre tanımlanır,
          çünkü banka işlemleri anlık değildir. Bu süre içinde ödeme ulaşmazsa rezervasyon
          iptal edilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="5. İptal ve iade">
        <p>
          Ödemesi tamamlanmamış rezervasyonu istediğin an kendin iptal edebilirsin. Ödemesi
          alınmış biletlerde iade, etkinliğin iptal politikasına göre yönetim tarafından
          yapılır; iade onaylandığında biletler geçersizleşir ve koltuklar satışa döner.
        </p>
        <p>
          Etkinlik düzenleyen tarafından iptal edilirse bilet bedeli iade edilir. Etkinlik
          tarihi veya salonu değişirse bu bilgi kayıtlı e-posta adresine bildirilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="6. Biletin kullanımı">
        <p>
          Her bilet tek kullanımlıktır. Kapıda QR kodu okutulduğu anda bilet kullanılmış
          olarak işaretlenir; aynı kod ikinci kez geçmez. Bileti çoğaltmak veya paylaşmak,
          girişi ilk okutana bırakır.
        </p>
        <p>
          Öğrenci bileti yalnızca doğrulanmış öğrencilik kaydı olan hesaplarda seçilebilir.
          Girişte öğrenci belgesi istenebilir; belgesi olmayan öğrenci bileti geçersiz
          sayılabilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="7. Kişisel veriler">
        <p>
          Hesap için ad-soyad ve e-posta, biletleme için rezervasyon ve ödeme kayıtları
          tutulur. Öğrenci doğrulamasında kurum ve öğrenci numarası saklanır; kimlik
          numarası zorunlu değildir ve verildiğinde uygulama günlüklerine hiçbir zaman
          yazılmaz. Günlüklerde e-posta ve telefon maskelenerek görünür.
        </p>
        <p>
          Veriler yalnızca biletleme, destek ve yasal saklama yükümlülüğü için kullanılır;
          üçüncü taraflara pazarlama amacıyla aktarılmaz. Ödeme sağlayıcısına yalnızca
          işlemin tamamlanması için gereken bilgiler iletilir. Verilerinin silinmesini veya
          bir kopyasını istemek için{' '}
          <Link to="/iletisim" className="text-primary hover:underline">
            İletişim
          </Link>{' '}
          sayfasındaki adrese yazabilirsin.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="8. Sorumluluk">
        <p>
          Etkinliğin içeriği, saati ve gerçekleşmesi düzenleyenin sorumluluğundadır. Loca,
          biletin satışı ve doğrulanmasından sorumludur. Planlı bakım, sağlayıcı arızası
          veya mücbir sebeplerden kaynaklanan kesintilerde hizmet geçici olarak
          durabilir.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="9. Değişiklikler">
        <p>
          Bu koşullar güncellenebilir. Rezervasyonuna, rezervasyonu açtığın tarihte geçerli
          olan koşullar uygulanır.
        </p>
      </BilgiBolumu>
    </BilgiSayfasi>
  );
}
