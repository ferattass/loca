import { Link } from 'react-router-dom';

import { BilgiBolumu, BilgiSayfasi } from './BilgiSayfasi';

/**
 * Yardim merkezi.
 *
 * Icerik uydurulmadi: her madde sistemin gercekten yaptigi seyi anlatiyor —
 * koltuk kilidi ve tek seferlik uzatma, oturum basina koltuk siniri,
 * ogrenci dogrulamasi, QR'in tek kullanimlik olmasi. Genel bir SSS metni
 * yazilsaydi, kullanici burada okudugunu ekranda bulamazdi.
 *
 * <para>
 * <b>Sureler sayiyla yazilmadi.</b> Kilit suresi, uzatma ve koltuk siniri
 * sunucu ayarlarindan geliyor ve panelden degistirilebiliyor; buraya "on
 * dakika" yazilsaydi ayar degistigi gun bu sayfa sessizce yalan soylerdi.
 * Kullanicinin gordugu gercek sure zaten ekrandaki geri sayimda.
 * </para>
 */
export function YardimPage() {
  return (
    <BilgiSayfasi
      baslik="Yardım merkezi"
      ozet="Bilet alma, koltuk tutma süresi, iptal ve bilet kullanımıyla ilgili sık sorulanlar."
    >
      <BilgiBolumu baslik="Nasıl bilet alırım?">
        <p>
          Ana sayfadan bir etkinlik seç, oturumu belirle ve salon planından koltuklarını
          işaretle. Seçimi onayladığında koltuklar sana ayrılır ve bir geri sayım başlar;
          bu süre içinde ödemeyi tamamlarsan biletlerin{' '}
          <Link to="/biletlerim" className="text-primary hover:underline">
            Biletlerim
          </Link>{' '}
          sayfasına düşer.
        </p>
        <p>
          Salonun doluluğunu görmek için giriş yapman gerekmiyor. Giriş yalnızca koltuk
          tutma adımında isteniyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Koltuğu ne kadar tutabilirim?">
        <p>
          Koltuklar seçildiği anda sana kilitlenir ve rezervasyon ekranında kalan süre
          saniye saniye görünür. Süre dolmadan <strong>bir kez</strong> uzatma hakkın var;
          uzattıktan sonra buton kapanır.
        </p>
        <p>
          Süre biterse koltuklar otomatik olarak satışa döner ve rezervasyon "Süresi doldu"
          durumuna geçer. Bu, koltuğun başkasına satılmasını engelleyen tek şeyin ödeme
          olması demek — ekranı açık bırakmak koltuğu tutmuyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Aynı oturumdan kaç koltuk alabilirim?">
        <p>
          Bir oturumda tek kullanıcının tutabileceği koltuk sayısı sınırlı. Sınırı aşan bir
          seçim yaptığında sunucu isteği reddediyor ve ekranda kaç koltuk daha
          alabileceğin yazıyor. Sınır, bir kişinin bütün salonu bloke etmesini önlemek
          için var.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Biletim nerede?">
        <p>
          Ödeme tamamlandığında biletler{' '}
          <Link to="/biletlerim" className="text-primary hover:underline">
            Biletlerim
          </Link>{' '}
          sayfasında etkinliğe göre gruplanmış olarak listelenir. Her biletin bir QR kodu
          var; PDF olarak indirip telefonda ya da basılı olarak getirebilirsin.
        </p>
        <p>
          Kapıda QR okutulduğunda bilet <strong>kullanılmış</strong> olarak işaretlenir ve
          aynı kod ikinci kez geçmez. Bu yüzden bileti başkasıyla paylaşman, kendi girişini
          riske atmak demek.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Öğrenci bileti">
        <p>
          Öğrenci bilet türü seçilebilmesi için hesabında doğrulanmış bir öğrencilik kaydı
          olması gerekiyor. Kurum adı ve öğrenci numarası yeterli; kimlik numarası zorunlu
          değil. Doğrulama tamamlanmadan öğrenci bileti seçilirse rezervasyon açılmıyor.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="İptal ve iade">
        <p>
          Ödemesi tamamlanmamış bir rezervasyonu{' '}
          <Link to="/rezervasyonlarim" className="text-primary hover:underline">
            Rezervasyonlarım
          </Link>{' '}
          sayfasından kendin iptal edebilirsin; koltuklar anında satışa döner.
        </p>
        <p>
          Ödemesi alınmış bir bilet için iade işlemini yönetim yapıyor. İade
          onaylandığında biletler iptal edilir ve koltuklar tekrar satışa açılır. İade
          talebini{' '}
          <Link to="/iletisim" className="text-primary hover:underline">
            İletişim
          </Link>{' '}
          sayfasındaki adrese, rezervasyon numaranı yazarak iletebilirsin.
        </p>
      </BilgiBolumu>

      <BilgiBolumu baslik="Şifremi unuttum">
        <p>
          Giriş ekranındaki{' '}
          <Link to="/sifremi-unuttum" className="text-primary hover:underline">
            Şifremi unuttum
          </Link>{' '}
          bağlantısı e-posta adresine tek kullanımlık bir sıfırlama bağlantısı gönderir.
          Bağlantı bir saat geçerli ve bir kez kullanılabiliyor; ikinci kez açıldığında
          çalışmaz. Şifre değiştiğinde açık olan bütün oturumlar kapanır.
        </p>
      </BilgiBolumu>
    </BilgiSayfasi>
  );
}
