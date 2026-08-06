# Uçtan uca doğrulama betikleri

Gerçek istek, gerçek veritabanı, gerçek Redis. Ayağa kalkmış bir API'ye HTTP üzerinden
bağlanırlar; sahte nesne (mock) kullanmazlar.

Depoda duruyorlar çünkü Gün 5'in betikleri geçici klasörde bırakılmış ve kaybolmuştu.

## Çalıştırma

```bash
docker-compose up -d

# Kilit süresini kısaltarak çalıştır: süre dolumu testi 10 dakika beklemesin.
Reservation__LockMinutes=1 Reservation__ExpirySweepSeconds=5 \
  dotnet run --project src/Loca.WebApi --urls http://localhost:5000

pip install requests
python tests/e2e/e2e_gun6.py            # rezervasyon ve eşzamanlılık (~2 dk)
python tests/e2e/e2e_gun6.py --hizli    # süre dolumu beklemesini atla
python tests/e2e/e2e_gun7.py            # ödeme, bilet, outbox
python tests/e2e/e2e_ayarlar.py         # ayarlar, SMTP, şifre sıfırlama postası
python tests/e2e/yaris_redis_kapali.py  # yarış durumu: Redis açık ve kapalı
python tests/e2e/yetki_denetimi.py      # yetki matrisi (hızlı, ~5 sn)
```

Her betik başarısız kontrol varsa sıfırdan farklı çıkış kodu döner.

### Kendi ortamını isteyen iki betik

Bu ikisi yukarıdaki API ile **koşmaz**; uygulamanın farklı yapılandırılmasını
istiyorlar ve sebebi ikisinde de aynı: sınanan şey ancak o yapılandırmada ortaya
çıkıyor.

```bash
# Güvenlik: hız sınırı geliştirmede bilerek geniş (üretim değerleri yarış
# testini kırıyor — tek IP'den 50 kayıt). Sınırı aşabilmek için daraltılıyor.
RateLimit__Auth__PermitLimit=10 RateLimit__Auth__WindowSeconds=60 \
RateLimit__PasswordReset__PermitLimit=3 RateLimit__PasswordReset__WindowSeconds=900 \
  dotnet run --project src/Loca.WebApi --urls http://localhost:5000
python tests/e2e/e2e_guvenlik.py

# iyzico: sağlayıcı Mock'tan Iyzico'ya alınıyor. Sahte anahtarla da anlamlı —
# isteğin gerçekten çıktığını kanıtlıyor.
Payment__Provider=Iyzico Iyzico__ApiKey=sandbox-... Iyzico__SecretKey=sandbox-... \
  dotnet run --project src/Loca.WebApi --urls http://localhost:5000
python tests/e2e/iyzico_dogrulama.py
```

> **Neden hız sınırı geliştirmede kapatılmadı:** kapalı olsaydı sınırlama yalnızca
> üretimde çalışır ve hiçbir yerde denenmemiş olurdu. Ödeme sağlayıcısında tam olarak
> bu tuzağa düşülmüştü — test edilen yol ile üretimde çalışacak yol aynı değildi.

> **`ExpirySweepSeconds` bir dönem ölü ayardı.** Gün 6'da bir `BackgroundService`
> onu okuyordu; iş Gün 7'de Hangfire'a taşınınca sabit `Cron.Minutely` yazıldı ve ayar
> hiçbir yerden okunmaz oldu — ama bu belgede ve `appsettings`'te durmaya devam etti.
> Yani yukarıdaki komutu çalıştıran biri sessizce yalnızca kilit süresini kısaltıyordu.
> 6 Ağustos'ta düzeltildi: değer hem cron ifadesine hem Hangfire'ın yoklama aralığına
> bağlandı (yalnızca cron'u değiştirmek yetmiyordu — iş yine 15 saniyede bir koşardı).

## `yetki_denetimi.py` ne yapıyor

Diğerleri "akış çalışıyor mu" diye bakar; bu betik **"yetkisi olmayan gerçekten
giremiyor mu"** diye bakar. Her ucu anonim, müşteri, organizatör ve admin oturumuyla
ayrı ayrı deneyip beklenen durum kodunu karşılaştırır.

Ayrıca sınır davranışlarını sınar: sayfa boyutu üst sınırı, negatif sayfa numarası,
geçersiz rol adı, kendi admin rolünü kaldırma, işlenmiş mesajı kuyruğa geri koyma.

Kuyruk yanıtının mesaj **gövdesini taşımadığını** da doğrular — gövde kişisel veri
içeriyor ve bir gün yanlışlıkla DTO'ya eklenirse bu kontrol yakalar.

## `yaris_redis_kapali.py` neden ayrı

`e2e_gun6.py` içindeki yarış testi Redis açıkken çalışıyor ve 49 çakışmanın tamamını
Redis ön elemesi yakalıyor — yani kabul ölçütü geçiyor ama **veritabanı savunması hiç
sınanmıyor**.

Bu betik aynı testi Redis kapalıyken tekrarlar. Beklenen: yine tam olarak 1 başarı, ve
409'ların bir kısmı `Reservation.SeatTakenConcurrently` kodunu taşımalı. Taşımıyorsa
eşzamanlılık damgası devrede değil demektir.

Ayrıca şartnamenin "cache kapalıyken sistem çalışmaya devam etmeli" maddesini doğrular.

## Neden Python

Bu betikler entegrasyon testi değil **kabul testi**: uygulamayı dışarıdan, bir istemcinin
gördüğü gibi denerler. 50 paralel isteği aynı anda göndermek ve Redis konteynerini işin
ortasında durdurmak için süreç dışında olmaları gerekiyor.

`tests/Loca.IntegrationTests` içindeki .NET testleri (Gün 10) bunların yerine geçmez;
farklı seviyeyi ölçerler.
