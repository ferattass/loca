import { Link } from 'react-router-dom';

import { BilgiBolumu, BilgiSayfasi } from './BilgiSayfasi';

/**
 * Gizlilik ve kisisel veriler.
 *
 * <b>Kullanim kosullarindan AYRI sayfa.</b> Kosullar sozlesme metni;
 * burasi "benim hangi verim nerede" sorusunun cevabi ve kullanici bunu
 * dokuz maddelik bir sozlesmenin yedinci basligi altinda aramak zorunda
 * kalmamali.
 *
 * <para>
 * Metin sistemin GERCEKTEN sakladigi alanlari sayiyor. Hazir bir gizlilik
 * sablonu cerez, reklam kimligi ve konum verisi gibi hic toplamadigimiz
 * seyleri de listelerdi; toplanmayan bir veriyi "isliyoruz" diye yazmak
 * da yanlis beyan.
 * </para>
 */
export function GizlilikPage() {
  return (
    <BilgiSayfasi
      baslik="Gizlilik ve kişisel veriler"
      ozet="Hangi verilerin tutulduğu, neden tutulduğu ve nasıl sildirileceği."
    >
      <BilgiBolumu baslik="Hangi veriler tutuluyor?">
        <p>
          <strong>Hesap için:</strong> ad-soyad, e-posta ve (verdiysen) telefon. E-posta aynı
          zamanda giriş kimliğin.
        </p>
        <p>
          <strong>Biletleme için:</strong> rezervasyonların, seçtiğin koltuklar, biletlerin ve
          ödeme kayıtların (tutar, tarih, sonuç ve sağlayıcı referansı).
        </p>
        <p>
          <strong>Öğrenci bileti için:</strong> kurum adı ve öğrenci numarası. Kimlik numarası
          zorunlu değil; verirsen yalnızca doğrulama için saklanır ve uygulama günlüklerine
          hiçbir zaman yazılmaz.
        </p>
        <p>
          <strong>Güvenlik için:</strong> oturum belirteçleri ve şifre sıfırlama kayıtları.
          Şifren düz metin olarak hiçbir yerde durmuyor; geri çevrilemeyen bir özet olarak
          saklanıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Kart bilgisi">
        <p>
          <strong>Kart numarası, son kullanma tarihi ve CVV bilgisi Loca sunucularına hiç
          ulaşmıyor.</strong> Ödeme, lisanslı sağlayıcının kendi sayfasında tamamlanıyor; bize
          yalnızca işlemin sonucu ve referans numarası dönüyor. Bu bir tercih değil akışın
          yapısı — saklamamak için ayrıca bir şey yapmamıza gerek yok, veri hiç gelmiyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Günlük kayıtları">
        <p>
          Hata ayıklama ve güvenlik için istek günlükleri tutuluyor. Bu kayıtlarda e-posta ve
          telefon <strong>maskelenmiş</strong> görünüyor (örnek: <code
            className="font-mono">fe*******@loca.dev</code>). Şifre sıfırlama bağlantısındaki
          kod, kimlik numarası ve ödeme sağlayıcısı anahtarları günlüğe hiçbir koşulda
          yazılmıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Kimlerle paylaşılıyor?">
        <p>
          Ödeme sağlayıcısına, işlemin tamamlanması için gereken bilgiler iletiliyor: ad,
          e-posta, tutar ve etkinlik adı. Sağlayıcının dolandırıcılık kontrolü bu bilgilere
          bakıyor; sabit yer tutucularla gönderilseydi her işlemi aynı kişi sanardı.
        </p>
        <p>
          Bunun dışında veriler üçüncü taraflara <strong>pazarlama amacıyla aktarılmıyor</strong>.
          Reklam ağı, izleme çerezi ve analiz betiği kullanılmıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Ne kadar süre saklanıyor?">
        <p>
          Bilet ve ödeme kayıtları mali kayıt sayıldığı için hesap kapatılsa bile yasal saklama
          süresi boyunca tutuluyor. Hesap bilgileri ve öğrenci doğrulaması, hesabın silinmesi
          talebinde kaldırılıyor. Şifre sıfırlama bağlantıları bir saat sonra, tek kullanımdan
          sonra ise anında geçersizleşiyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Haklarım ve nasıl kullanacağım">
        <p>
          Verilerine erişmeyi, düzeltilmesini, bir kopyasını almayı veya silinmesini
          isteyebilirsin.{' '}
          <Link to="/iletisim" className="text-primary hover:underline">
            İletişim
          </Link>{' '}
          sayfasındaki adrese kayıtlı e-postandan yazman yeterli.
        </p>
        <p>
          Şifreni değiştirdiğinde açık olan tüm oturumlar kapanıyor; cihazını kaybettiğinde
          yapman gereken ilk şey bu.
        </p>
      </BilgiBolumu>
    </BilgiSayfasi>
  );
}
