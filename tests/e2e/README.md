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
python tests/e2e/e2e_gun6.py            # tam senaryo (~2 dk, süre dolumu beklemesi dâhil)
python tests/e2e/e2e_gun6.py --hizli    # süre dolumu beklemesini atla
python tests/e2e/yaris_redis_kapali.py  # yarış durumu: Redis açık ve kapalı
```

Her betik başarısız kontrol varsa sıfırdan farklı çıkış kodu döner.

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
