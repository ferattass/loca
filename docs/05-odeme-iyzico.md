# Ödeme sağlayıcısı — iyzico

Ödeme akışı `IPaymentService` arayüzünün arkasında duruyor. Uygulama üç
sağlayıcıyla çalışabiliyor; hangisinin kullanılacağı `Payment:Provider`
ayarından okunuyor:

| Değer | Sağlayıcı | Ne işe yarar |
|---|---|---|
| `Mock` (varsayılan) | `MockPaymentProvider` | Her ödeme başarılı. Geliştirme ve uçtan uca test. |
| `FailedMock` | `FailedPaymentProvider` | Her ödeme reddedilir. "Ödeme başarısızsa koltuklar serbest kalıyor mu" kuralı ancak böyle doğrulanabiliyor. |
| `Iyzico` | `IyzicoPaymentProvider` | Gerçek entegrasyon. Sandbox ve canlı aynı sınıfla çalışıyor. |

Sağlayıcı **başlangıçta bir kez** seçiliyor. İstek başına seçilseydi aynı
ödemenin başlatılması ve tamamlanması iki farklı sağlayıcıya düşebilirdi.

---

## Neden checkout form, doğrudan kart API'si değil

Checkout form akışında **kart bilgisi bizim sunucumuzdan hiç geçmiyor**:
kullanıcı kartını iyzico'nun kendi sayfasında giriyor, biz yalnızca bir
ödeme sayfası token'ı açıyor ve sonucu soruyoruz.

Bu, "kart verisini saklamayalım" kararını bir dikkat meselesi olmaktan
çıkarıp yapısal hâle getiriyor: saklamamak için ayrıca uğraşmaya gerek
yok, veri hiç gelmiyor. `IPaymentService` sözleşmesinde de bilerek kart
parametresi yok — arayüze bir kez kart alanı eklenseydi, sonradan onu
loglamamak için her çağrı yolunun ayrı ayrı gözden geçirilmesi
gerekirdi.

Resmî `Iyzipay` NuGet paketi **kullanılmadı**: senkron `HttpWebRequest`
kullanıyor ve `IHttpClientFactory` ile yönetilmiyor (bağlantı havuzu,
zaman aşımı, yeniden deneme politikaları paketin dışında kalıyor).
İmzalama (IYZWSv2) bunun yerine `IyzicoSignature` içinde elle uygulandı.

---

## Sandbox kurulumu

### 1. Sandbox hesabı aç

<https://sandbox-merchant.iyzipay.com/auth/register> adresinden kayıt ol.
Gerçek bir şirket bilgisi gerekmiyor.

### 2. API anahtarlarını al

Panelde **Ayarlar → Firma Ayarları**, sayfanın altındaki **API Anahtarları**
bölümünde "Görüntüle". İki değer var:

- `API KEY` — `sandbox-...` ile başlıyor
- `SECRET KEY` — `sandbox-...` ile başlıyor

### 3. user-secrets'a yaz

**Anahtarlar depoya girmiyor.** `appsettings.json`'a yazılsaydı ilk
commit'te GitHub'a çıkardı; sandbox anahtarı bile olsa alışkanlık yanlış
olurdu.

```bash
cd src/Loca.WebApi

dotnet user-secrets set "Payment:Provider" "Iyzico"
dotnet user-secrets set "Iyzico:ApiKey" "sandbox-..."
dotnet user-secrets set "Iyzico:SecretKey" "sandbox-..."
dotnet user-secrets set "Iyzico:UseSandbox" "true"
dotnet user-secrets set "Iyzico:CallbackUrl" "https://<tünel-adresi>/api/v1/payments/iyzico/callback"
dotnet user-secrets set "Iyzico:ReturnUrl" "http://localhost:5173"
```

`UseSandbox` varsayılanı `true`. Yapılandırma unutulursa yanlışlıkla canlı
tahsilat yapmak yerine sandbox'ta kalınır; güvenli varsayılan bu yönde.

### 4. Callback için tünel

iyzico ödeme bittiğinde tarayıcıyı `CallbackUrl` adresine **form POST**
ile gönderiyor. `localhost` iyzico tarafından çözülemediği için yerel
geliştirmede bir tünel gerekiyor:

```bash
ngrok http 5000
# çıkan https adresini CallbackUrl'e yaz
```

Tünel adresi her açılışta değişiyorsa `CallbackUrl`'i de güncellemek
gerekiyor.

---

## Akış

```
1. Arayüz          POST /api/v1/payments            → ödeme kaydı açılır (Pending)
2. Sunucu          iyzico checkout form initialize  → token + paymentPageUrl
3. Arayüz          paymentPageUrl'e yönlendirir     → kullanıcı kartını iyzico'da girer
4. iyzico          POST /payments/iyzico/callback   → tarayıcı üzerinden, token ile
5. Sunucu          302 → /odeme/{rezervasyonId}?odemeId=...
6. Arayüz          POST /payments/{id}/complete     → kendi oturumuyla
7. Sunucu          iyzico'ya "bu ödeme ne oldu" diye SORAR → biletler üretilir
```

**4. adım ödemeyi kapatmıyor.** iyzico'nun geri dönüşü tarayıcı üzerinden
geliyor ve o istekte bizim oturum belirtecimiz yok. Kapatma yetkisi için
oturum kontrolü gevşetilseydi, token'ı ele geçiren biri başkasının
ödemesini kapatabilirdi. Callback yalnızca "bu token hangi rezervasyona
ait" sorusunu cevaplayıp tarayıcıyı arayüze geri gönderiyor.

**7. adımda sonuç sağlayıcıya soruluyor**, callback'in söylediğine
güvenilmiyor. Bildirim kaybolabilir, gecikebilir veya taklit edilebilir;
biletin üretilip üretilmeyeceği kararı yalnızca iyzico'nun kendi
cevabına dayanıyor.

> **Bilinen açık.** Kullanıcı ödemeden sonra tarayıcıyı kapatırsa 6. adım
> hiç çağrılmaz: ödeme `Pending` kalır, rezervasyon süresi dolunca
> koltuklar serbest bırakılır, para sağlayıcıda bekler ve mutabakatta
> görünür. Kalıcı çözüm bekleyen ödemeleri sağlayıcıya soran zamanlanmış
> bir iş; Hangfire altyapısı hazır, iş henüz yazılmadı.

---

## Test kartları

Sandbox'ta **gerçek kart çalışmıyor**, yalnızca test kartları kabul
ediliyor. Son kullanma tarihi ve CVC serbest — doğru biçimde ve ileri bir
tarih olması yeterli (örn. `12/30`, `123`).

**3D Secure SMS şifresi sandbox'ta her zaman `123456`.**

### Başarılı

| Kart | Banka | Tip |
|---|---|---|
| `5890040000000016` | Akbank | Master Card, banka kartı |
| `4766620000000001` | Denizbank | Visa, banka kartı |
| `5311570000000005` | QNB | Master Card, kredi kartı |
| `5170410000000004` | Garanti | Master Card, banka kartı |

### Hata senaryoları

| Kart | Sonuç |
|---|---|
| `4111111111111129` | Yetersiz bakiye |
| `4126111111111114` | Çalıntı kart |
| `4124111111111116` | Geçersiz CVC2 |

Hata kartları `FailedMock` sağlayıcısının yerini tutmuyor: o sağlayıcı
ağa hiç çıkmadan reddediyor ve testlerde deterministik. Hata kartları
gerçek sağlayıcı cevabının doğru yorumlanıp yorumlanmadığını gösteriyor.

---

## Müşteri bilgisi

iyzico checkout form açarken alıcı adı, e-postası, IP'si ve kimlik
numarası istiyor. Bunlar **kart verisi değil**, dolandırıcılık
puanlamasında kullanılan kimlik alanları.

- **Ad, soyad, e-posta** kullanıcı kaydından geliyor.
- **IP** bağlantıdan okunuyor, `X-Forwarded-For` başlığından değil: o
  başlığı istemci de gönderebiliyor, doğrudan okunması adresi
  uydurulabilir yapardı. Vekil sunucu arkasına alındığında doğru çözüm
  `ForwardedHeaders` ara yazılımını güvenilen vekil listesiyle
  yapılandırmak.
- **Kimlik numarası** toplanmıyor; iyzico'nun dokümantasyonundaki örnek
  değer gönderiliyor. Yalnızca bu alan için TC kimlik numarası toplamak,
  veriyi ihtiyaçtan fazla toplamak olurdu.
- **Adres** alanları zorunlu ama satılan şey dijital bir bilet; teslimat
  adresi diye bir şey yok. İletişim adı gerçek, adres satırı
  "Belirtilmedi" — uydurma bir sokak adresi kayıtları kirletirdi.

> İlk sürümde bu alanların tamamı sabit yer tutucuydu ("Musteri Musteri",
> `1.1.1.1`). Yer tutucuyla çalışan bir dolandırıcılık puanlaması her
> işlemi aynı kişi sanar; canlıya çıkmadan kapatılması gereken bir
> açıktı.

---

## İade ve iptal

iyzico iki farklı kimlik istiyor: **iptal** için `paymentId`, **iade**
için `paymentTransactionId`. `IPaymentService` tek bir `Reference` alanı
taşıdığı için ikisi `|` ile birleştirilip tek alanda tutuluyor ve
gerektiğinde ayrıştırılıyor (`ReferansParcala`).

Bu bir taviz: temizi arayüze iki ayrı alan koymak olurdu ama o zaman
taklit sağlayıcının da anlamsız bir ikinci kimlik uydurması gerekirdi.
Sağlayıcıya özgü bir ayrıntının sözleşmeye sızmaması tercih edildi.

---

## Sırların loglanmaması

`SecretKey` yalnızca HMAC anahtarı olarak kullanılıyor; hiçbir log
satırına, hata mesajına veya `PaymentResult.FailureReason` içine
yazılmıyor.

iyzico'nun ham cevabı da hiçbir yerde olduğu gibi taşınmıyor: yanıt
sınıfları kasıtlı olarak dar tutuldu — yalnızca `status`, `errorCode`,
`errorMessage` ve işlem kimlikleri tanımlı. Kart maskesi, BIN, kart tipi
gibi alanlar bu sınıflarda **hiç tanımlı olmadığı için** JSON'da gelseler
bile nesneye taşınmıyor, dolayısıyla loglara da sızamıyor.

---

## Canlıya geçiş

```bash
dotnet user-secrets set "Iyzico:UseSandbox" "false"
dotnet user-secrets set "Iyzico:ApiKey" "<canli-anahtar>"
dotnet user-secrets set "Iyzico:SecretKey" "<canli-anahtar>"
```

Taban adres koda gömülmedi; `UseSandbox` değerine göre
`sandbox-api.iyzipay.com` veya `api.iyzipay.com` seçiliyor. Ortam
değişikliğinde deploy edilen kod değil yalnızca yapılandırma değişiyor.

Canlıda ayrıca `CallbackUrl` ve `ReturnUrl` gerçek alan adlarını
göstermeli ve **ikisi de HTTPS** olmalı.
