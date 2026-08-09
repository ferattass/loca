# 06 · Canlıya alma (Render)

Bu belge Loca'yı Render'ın ücretsiz katmanında ayağa kaldırmanın adımlarını
anlatır. Blueprint (`render.yaml`) ve tek konteynerlik `Dockerfile` depoda
hazır; burada yalnızca panelde yapılacaklar var.

> Aşağıdaki akış, imaj yerelde birebir aynı biçimde çalıştırılıp
> doğrulandıktan sonra yazıldı: `/health` 200, SPA kökü ve derin yolları 200,
> tanımsız API ucu 404, kimlik doğrulama 401, üretimde Swagger kapalı,
> güvenlik başlıkları yerinde, Redis kapalıyken hazırlık ucu `Degraded`
> dönüyor ama uygulama ayakta kalıyor.

---

## 0. Önce bilinmesi gerekenler

Ücretsiz katmanın dört sınırı demoyu doğrudan etkiliyor:

| Sınır | Sonuç | Ne yapmalı |
|---|---|---|
| Servis 15 dk sessiz kalınca uyuyor | İlk istek ~1 dk bekliyor | Demodan önce adresi bir kez aç |
| Kalıcı disk yok | Yüklenen afiş/sözleşmeler her dağıtımda siliniyor | Dağıtım sonrası `tools/demo-veri/afisleri_yenile.py` |
| Çalışma alanı başına tek ücretsiz Postgres | Blueprint kendi veritabanını **oluşturmuyor** | Veritabanını elle aç, `DATABASE_URL`'i elle gir |
| SMTP portları kapalı | Şifre sıfırlama postası **gitmiyor** | Bağlantıyı elle ilet; kalıcı çözüm HTTP tabanlı sağlayıcı (Resend/SendGrid API) |

Ücretsiz Postgres **30 gün sonra** doluyor (silinmeden önce 14 gün yükseltme
süresi var). Staj teslimi bu pencerenin içindeyse sorun değil; daha uzun
ayakta kalacaksa ücretli katman ya da düzenli `pg_dump` gerekiyor.

---

## 1. Hesap ve veritabanı

1. [render.com](https://render.com) → **Get Started** → **GitHub ile giriş**
   (aynı hesap `ferattass/loca` deposunu görebilmeli).
2. Panelde **New → Postgres**:
   - Name: `loca-postgres`
   - Database: `loca`
   - Plan: **Free**
3. Veritabanı hazır olunca sayfasındaki **Internal Database URL** değerini
   kopyala. Dış adres değil **iç** adres kullanılmalı: dış adres internetten
   geçiyor ve ücretsiz katmanda belirgin biçimde yavaş.

Biçim şöyle görünür:

```
postgres://loca_user:xxxxxxxx@dpg-xxxxx-a/loca
```

Uygulama bunu açılışta Npgsql biçimine kendisi çeviriyor
(`BarindirmaAyarlari`) ve üretimde `SSL Mode=Require` uyguluyor.

---

## 2. Blueprint

1. **New → Blueprint**
2. `ferattass/loca` deposunu seç, dal **`main`**
3. Render `render.yaml`'ı okuyup `loca` adında bir web servisi önerecek
4. Panel `sync: false` işaretli üç değeri soracak:

| Değişken | Ne girilecek |
|---|---|
| `DATABASE_URL` | 1. adımda kopyalanan **Internal Database URL** |
| `AdminSeed__Email` | İlk yönetici hesabının e-postası |
| `AdminSeed__Password` | En az 8 karakter, büyük/küçük harf + rakam + simge |

`Jwt__Secret` sorulmaz — Render rastgele üretir ve kimse görmez. Elle
girilen bir anahtar panel geçmişinde ve panoda kalırdı.

5. **Apply / Deploy**

İlk dağıtım imajı sıfırdan derliyor (arayüz + .NET yayını): **8–12 dakika**
sürmesi normal.

---

## 3. Dağıtım sonrası doğrulama

Adres `https://loca-xxxx.onrender.com` biçiminde çıkar. Sırayla:

```bash
ADRES=https://loca-xxxx.onrender.com

curl -s -o /dev/null -w "%{http_code}\n" $ADRES/health            # 200
curl -s -o /dev/null -w "%{http_code}\n" $ADRES/api/v1/ping       # 200
curl -s -o /dev/null -w "%{http_code}\n" $ADRES/                  # 200  (SPA)
curl -s -o /dev/null -w "%{http_code}\n" $ADRES/biletlerim        # 200  (derin yol)
curl -s -o /dev/null -w "%{http_code}\n" $ADRES/api/v1/yok        # 404  (SPA yutmuyor)
curl -s -o /dev/null -w "%{http_code}\n" $ADRES/swagger/index.html # 404 (üretimde kapalı)
curl -s $ADRES/health/hazir
```

Son satır şunu döndürmeli — **Redis'in `Degraded` olması beklenen durum**,
ücretsiz katmanda bilerek Redis kurulmuyor:

```json
{"durum":"Degraded","kontroller":[{"ad":"veritabani","durum":"Healthy"},
                                  {"ad":"redis","durum":"Degraded"}]}
```

Redis kapalıyken koltuk çakışması veritabanı savunmasıyla (kısmi tekil index
+ eşzamanlılık damgası) engelleniyor — Gün 6'daki katmanlı savunma kararı
tam olarak bunun içindi.

Sonra tarayıcıdan `AdminSeed` bilgileriyle giriş yap ve **Yönetim → Özet**
ekranının açıldığını gör.

---

## 4. Katalog görselleri

Kalıcı disk olmadığı için afişler dağıtımla birlikte gitti. Yerelden:

```bash
LOCA_SUNUCU=https://loca-xxxx.onrender.com python tools/demo-veri/afisleri_yenile.py
```

Katalog tamamen boşsa önce `tools/demo-veri/etkinlikleri_doldur.py`.

---

## 5. Ödemeyi gerçek iyzico sandbox'ına almak (isteğe bağlı)

Blueprint `Payment__Provider=Mock` ile geliyor: ödeme akışı uçtan uca
çalışıyor ama sağlayıcıya çıkmıyor. Gerçek sandbox akışı için:

1. Render → servis → **Environment** → `Payment__Provider` değerini
   `Iyzico` yap
2. Uygulamada **Yönetim → Ödeme ayarları**:
   - iyzico API anahtarı ve gizli anahtarı
   - Callback adresi: `https://loca-xxxx.onrender.com/api/v1/payments/iyzico/callback`

Anahtarlar ortam değişkeni olarak değil panelden giriliyor ve şifreli
saklanıyor; şifreleme anahtarları da **veritabanında** duruyor (diskte
kalsalardı her uyanışta yenisi üretilir ve girilen anahtarlar çözülemez
hâle gelirdi — üstelik bu hata sessiz olurdu).

> **Henüz kanıtlanmamış:** iyzico callback'i hiç denenmedi. Ödemenin
> *başlatılması* doğrulandı (gerçek `paymentPageUrl` dönüyor),
> *tamamlanması* denenmedi. Render'da gerçek bir HTTPS adresi olduğu için
> artık tünel gerekmiyor; deneme buradan yapılabilir.

---

## 6. Sık karşılaşılanlar

**Dağıtım "cannot have more than one active free tier database" ile düşüyor**
Çalışma alanında zaten başka bir ücretsiz Postgres var. Ya onu sil ya da
mevcut olanı paylaş: Loca tablolarını EF ile oluşturuyor ve kendi göç
kaydını (`__EFMigrationsHistory`) tuttuğu için başka bir aracın tablolarıyla
çakışmıyor.

**İlk istek 30–60 saniye sürüyor**
Servis uyumuş. Beklenen davranış, hata değil.

**Loglarda `__EFMigrationsHistory` okuma hatası**
İlk açılışta normal: tablo henüz yok, EF ardından oluşturuyor. Bunu
takip eden `Admin hesabi olusturuldu` satırı her şeyin yolunda olduğunu
gösterir.

**Şifre sıfırlama postası gelmiyor**
SMTP portları ücretsiz katmanda kapalı (0. bölüm). Beklenen sınır.
