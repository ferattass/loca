# Eşzamanlılık Kararı

> Tarih: 24.07.2026 · Gün 1 (taslak) — uygulama Gün 6

Şartname şunu istiyor: *"Stajyerler seçtikleri yöntemin avantajlarını ve dezavantajlarını yazılı olarak açıklamalıdır."* Bu belge o açıklamadır.

## 1. Problem

İki kullanıcı aynı anda aynı koltuğu seçiyor. Naif çözüm şöyle görünür:

```
koltuğu oku
eğer durum "boş" ise          ← A ve B ikisi de buradan geçer
    durumu "kilitli" yap
    kaydet                     ← ikisi de başarılı olur
```

Kontrol ile yazma arasında bir boşluk var. Bu aralıkta ikinci istek de aynı "boş" değerini okur. Sonuç: iki kullanıcı da ödeme yapar, etkinlik gününde aynı koltuğa iki bilet çıkar.

Bu, "transaction kullanıyorum, güvendeyim" denilerek atlanan klasik hatadır. Transaction tek başına yetmez; sorun **okuma ile yazma arasındaki boşluktur**.

## 2. Dört yöntem

| Yöntem | Nasıl çalışır | Avantaj | Dezavantaj |
|---|---|---|---|
| **Optimistic concurrency** | Satır okunurken sürüm numarası alınır; yazarken sürüm değişmişse hata verir | Kilit tutmaz, okuma hiç bloklanmaz, yüksek eşzamanlılıkta ölçeklenir | Çakışma olduğunda işlem baştan denenmeli; çakışma çoksa boşa iş artar |
| **Pessimistic lock** (`SELECT ... FOR UPDATE`) | Satır transaction boyunca kilitlenir | Kesin sonuç, veritabanı garantisi | Bağlantı ve kilit tutar; deadlock riski; uzun transaction tüm sistemi yavaşlatır |
| **Redis distributed lock** | `SET key val NX PX ttl` ile atomik kilit alınır | Çok hızlı, veritabanını yormaz, TTL ile kendiliğinden temizlenir | Redis düşerse koruma kalkar; TTL erken dolarsa iki sahip oluşabilir; ek altyapı bağımlılığı |
| **Unique constraint** | Veritabanı ikinci kaydı reddeder | En basit, en güvenilir, atomik; ek altyapı gerektirmez | Yalnızca yazma anında yakalar; hatayı anlamlı mesaja çevirmek gerekir |

## 3. Karar: katmanlı savunma

Tek yöntem seçilmedi. **Üçü birlikte** kullanılacak.

| Katman | Yöntem | Ne işe yarar | Düşerse ne olur |
|:---:|---|---|---|
| 1 | Redis TTL kilidi | Hızlı ön eleme; boşa veritabanı işi yapılmaz, kullanıcı anında geri bildirim alır | Katman 2 ve 3 devrede kalır — sistem yavaşlar ama **yanlış sonuç vermez** |
| 2 | DB transaction + concurrency token | Gerçek tutarlılık; okuma ile yazma arasında değişen satırı yakalar | Katman 3 devrede kalır |
| 3 | `UNIQUE(EventSessionId, SeatId)` | Son savunma; hiçbir şey kaçmaz | Veritabanı yoksa zaten sistem yok |

### Neden tek başına hiçbiri yetmiyor

- **Yalnız Redis:** Redis düşerse veya TTL erken dolarsa çift satış olur. Kilit veritabanı gerçeğini bilmez.
- **Yalnız optimistic:** Doğru çalışır ama her çakışmada veritabanına gidilir. Popüler bir konserde gereksiz yük.
- **Yalnız pessimistic:** Doğru ama 10 dakikalık bir kilit boyunca veritabanı bağlantısı tutulamaz.
- **Yalnız unique constraint:** Yakalar ama en son anda. Kullanıcı ödeme ekranına kadar gelip orada hata alır — kötü deneyim.

### En sık atlanan nokta

Redis kilidi alındıktan **sonra** veritabanında durum yeniden doğrulanmalı.

Redis'in kilidi vermesi, o koltuğun veritabanında hâlâ boş olduğunu garanti etmez: Redis yeniden başlamış olabilir, TTL beklenenden erken dolmuş olabilir, ya da başka bir yol (admin işlemi, süre dolumu job'ı) koltuğu değiştirmiş olabilir.

**Tek gerçek kaynak veritabanıdır.** Redis yalnızca hızlandırıcıdır.

## 4. Rezervasyon akışının adımları

1. Idempotency kontrolü — aynı `Idempotency-Key` ile kayıt varsa aynı sonucu dön
2. Kullanıcının o oturumdaki bilet sayısı limitini kontrol et (en fazla 6)
3. Seçilen koltuklar için Redis kilidi al — biri alınamazsa alınanları bırak, 409 dön
4. Veritabanı transaction'ı aç
5. **Koltukları yeniden oku ve durumlarını doğrula** — bu adım atlanamaz
6. Durumu `Locked`, `LockedUntil` = şimdi + 10 dakika
7. Rezervasyon ve kalemleri oluştur; **toplam tutarı sunucu hesaplar**
8. Kaydet — concurrency hatası gelirse 409
9. Commit
10. Redis kilitlerini bırak

Kilitler sabit sırada (koltuk kimliğine göre sıralı) alınacak. İki kullanıcı `{A, B}` ve `{B, A}` isterse, sabit sıra olmadan biri A'yı diğeri B'yi alır ve ikisi de sonsuza kadar bekler.

Kilit bırakma işlemi Lua script ile yapılacak: kilit kendi token'ımıza aitse sil, değilse dokunma. Aksi hâlde TTL dolmuşsa başkasının kilidi silinebilir.

## 5. Doğrulama

Karar, ölçülebilir bir testle doğrulanacak:

> **50 paralel istek aynı koltuğu istediğinde tam olarak 1 tanesi başarılı, 49 tanesi 409 almalı.** Veritabanında o koltuk için tek bir aktif rezervasyon bulunmalı.

Bu test entegrasyon testine dâhil edilecek ve her CI koşusunda çalışacak. Elle bir kez denenip geçilmeyecek — eşzamanlılık hatası ancak otomatik testle yakalanır.

Test 50/50 başarılı çıkarsa neredeyse kesinlikle 5. adım atlanmıştır.

## 6. Redis kapalıyken davranış

Şartname "cache kapalı olduğunda sistem çalışmaya devam edebilmelidir" diyor. Kilit servisi de aynı ilkeye uyacak: Redis'e erişilemezse hata loglanır, kilit alınamadı sayılmaz, akış katman 2 ve 3 ile devam eder.

Sonuç: sistem yavaşlar, çakışma oranı artar, ama **yanlış veri üretmez**.

---

# Uygulama ve Ölçüm Sonuçları

> Tarih: 03.08.2026 · Gün 6 — yukarıdaki kararın gerçekleşen hâli

## 7. Kararın neresi değişti

Plan büyük ölçüde olduğu gibi uygulandı. İki noktada düzeltme gerekti.

### 7.1 Üçüncü katman düşünüldüğü şey değilmiş

§3'te son savunma hattı `UNIQUE(EventSessionId, SeatId)` olarak yazılmıştı. Uygulanınca görüldü ki **bu kısıt rezervasyon yarışına hiç bakmıyor**: aynı koltuğun aynı oturumda iki `EventSeats` satırı olmasını engelliyor, yani *koltuk üretimini* koruyor. Rezervasyon sırasında yeni satır eklenmiyor, mevcut satır güncelleniyor — dolayısıyla o kısıt hiçbir zaman devreye girmiyor.

Rezervasyon yarışının gerçek son hattı **`xmin` eşzamanlılık damgası**. Katman tablosunun doğru hâli:

| Katman | Yöntem | Rezervasyonda ne yapıyor |
|:---:|---|---|
| 1 | Redis TTL kilidi | Hızlı ön eleme; veritabanına hiç gidilmez |
| 2 | Transaction içinde yeniden okuma (5. adım) | Redis'in göremediği durumu yakalar |
| 3 | `xmin` concurrency token | Okuma ile yazma arasında değişen satırı yakalar |
| — | `UNIQUE(EventSessionId, SeatId)` | Koltuk **üretimini** korur, rezervasyonu değil |

Kısıt yanlış değil, yeri yanlış anlatılmıştı.

### 7.2 Koltuk kilidi sahipsiz olamıyor

`EventSeat.Lock` başta yalnızca kullanıcı ve süre alıyordu. Süresi dolan kaydı temizleyen iş yazılırken çıktı: koltuk hangi rezervasyondan koparıldığını bilmiyor. Kilit ile rezervasyon iki ayrı adımda kurulsaydı, ikisi arasında bir hata oluştuğunda koltuk sahipsiz kilitli kalırdı. `Lock` artık rezervasyon kimliğini zorunlu alıyor.

## 8. Ölçüm: 50 paralel istek, aynı koltuk

Kabul ölçütü §5'te tanımlanmıştı. Test iki kez çalıştırıldı — ikinci koşu, ölçütün kendisindeki bir körlüğü ortaya çıkardı.

### 8.1 Redis açık

```
Süre          : 212 ms
200 (başarılı): 1
409 (çakışma) : 49
409 kodları   : {"Reservation.SeatNotAvailable": 49}
```

Ölçüt geçti. **Ama 49 çakışmanın tamamı Redis'te yakalandı** — yani veritabanı savunması hiç sınanmadı. Test yeşil, katman 2 ve 3 hakkında hiçbir şey söylemiyor.

Bu tam olarak Gün 5'te öğrenilen dersin tekrarı: *"doğru durum kodu döndü" testi geçirmeye yeter, doğrulamaya yetmez.*

### 8.2 Redis kapalı (`docker stop loca-redis`)

```
Süre          : 3058 ms
200 (başarılı): 1
409 (çakışma) : 49
409 kodları   : {"Reservation.SeatTakenConcurrently": 26,
                 "Reservation.SeatNotAvailable": 23}
```

Üç sonuç birden:

1. **Kabul ölçütü Redis olmadan da sağlanıyor** — tam olarak 1 başarı. Katmanlı savunma iddiası kanıtlandı.
2. **Her iki katman da çalışıyor.** 26 istek `xmin` damgasına (katman 3), 23 istek transaction içindeki yeniden okumaya (katman 2, 5. adım) takıldı. 5. adım atlansaydı bu 23 istek başarılı olur ve **aynı koltuk 24 kişiye satılırdı**.
3. **Redis saf hızlandırıcı.** 212 ms → 3058 ms, yaklaşık 14 kat. Doğruluk değişmiyor, yalnızca 49 boşa veritabanı işlemi yapılıyor.

Veritabanı kanıtı (her iki koşu için ayrı ayrı):

```sql
select count(*) from "ReservationItems" i
  join "Reservations" r on r."Id" = i."ReservationId"
 where i."EventSeatId" = '...' and r."Status" = 1;   -- 1
```

Koltuk durumu her iki koşuda da `Status = 2 (Locked)`, `LockedByUserId` dolu.

### 8.3 Ölçütün düzeltilmiş hâli

> 50 paralel istekte tam olarak 1 başarı — **hem Redis açıkken hem kapalıyken.** Redis kapalı koşuda 409'ların bir kısmı `SeatTakenConcurrently` kodunu taşımalı; taşımıyorsa veritabanı katmanı sınanmamış demektir.

Tek başına "Redis açıkken 1/50" ölçütü, Redis'in arkasına saklanan bir hatayı göremez.

## 9. Uygulama ayrıntıları

**Kilit anahtarı:** `loca:lock:session:{oturumId}:seat:{koltukId}` — biçim tek yerde (`SeatLockKeys`) üretiliyor. İki yerde elle yazılsaydı bir harflik fark, kilidin alınıp bırakılamamasına yol açardı.

**Sıralama:** anahtarlar ordinal sıraya sokulup öyle alınıyor. `{A,B}` ve `{B,A}` isteyen iki istek sabit sıra olmadan birbirini boşa düşürürdü.

**Bırakma:** Lua betiği ile, önce token karşılaştırılıyor. Düz `DEL` kullanılsaydı TTL dolup anahtarı başkası aldığında onun kilidi silinirdi.

**Redis'siz açılış:** bağlantı tembel kuruluyor ve `AbortOnConnectFail = false`. Bağlantı `Program.cs`'te kurulsaydı Redis kapalıyken uygulama hiç ayağa kalkmazdı — kilit servisi olmadan çalışabilen bir sistem, kilit servisi yüzünden tamamen dururdu.

**Katman sınırı:** `DbUpdateConcurrencyException` uygulama katmanında yakalanmıyor; Persistence, `SaveChanges` sınırında onu `ConcurrencyConflictException`'a çeviriyor. Aksi hâlde veritabanı teknolojisi iş kurallarının arasına girerdi.

**Idempotency:** `UNIQUE(UserId, IdempotencyKey)`. Uygulamadaki "bu anahtarla kayıt var mı" sorgusu yarışı kaybedebilir — iki istek aynı anda bakıp ikisi de boş bulabilir. Kısıt veritabanında olduğu için yalnızca biri yazabiliyor; kaybeden istek hatayı yakalayıp kazananın kaydını dönüyor.

## 10. Uçtan uca sonuç

`scratchpad/e2e_gun6.py` — gerçek istek, gerçek veritabanı, gerçek Redis.

Kapsanan başlıklar: anonim istek 401 · tutarın sunucuda hesaplanması · koltuk planında `Locked` + `isLockedByMe` · `lockedByUserId` sızmıyor · başkasının koltuğu 409 · aynı `Idempotency-Key` ile ikinci istek aynı kaydı dönüyor · başlık yoksa 400 · başkasının rezervasyonu 403 (404 değil) · uzatma bir kez, ikincisi 409 · iptal koltuğu anında serbest bırakıyor · serbest kalan koltuğu başkası alabiliyor · oturum başına 6 bilet limiti (tek istekte ve istekler arasında) · başka oturumun koltuğu 404 · satışı başlamamış oturum 409 · öğrenci doğrulaması satın almaya bağlandı (belgesiz 409, onaysız 409, onaylı 200) · 50 paralel yarış · süre dolumu.
