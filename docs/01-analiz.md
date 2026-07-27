# İş Analizi

> Tarih: 24.07.2026 · Gün 1

Bu belge, kod yazmadan önce sistemin ne yapacağını ve hangi kuralların geçerli olacağını sabitler. Şartnamedeki on altı iş analizi sorusu burada gerekçeleriyle cevaplanmıştır. Sonraki günlerde bir karar tartışmaya açıldığında referans bu belgedir.

## 1. Roller ve yetkiler

Sistemde üç rol var. Bir kullanıcı birden fazla role sahip olabilir (organizatör aynı zamanda bilet alabilir).

| İşlem | Kullanıcı | Organizatör | Admin |
|---|:---:|:---:|:---:|
| Etkinlik listeleme ve detay görüntüleme | + | + | + |
| Koltuk seçme, rezervasyon oluşturma | + | + | + |
| Ödeme yapma | + | + | + |
| Kendi biletlerini görüntüleme ve iptal etme | + | + | + |
| Favori yönetimi | + | + | + |
| Yorum ve puan verme | + | + | + |
| Bildirimleri görüntüleme | + | + | + |
| Etkinlik oluşturma | – | + | + |
| **Kendi** etkinliğini güncelleme | – | + | + |
| **Başkasının** etkinliğini güncelleme | – | **–** | + |
| Salon ve oturma planı seçme | – | + | + |
| Bilet türü ve fiyat tanımlama | – | + | + |
| Satış durumu ve rapor görüntüleme | – | + (kendi) | + (tümü) |
| Etkinliği yayına alma / iptal etme | – | + (kendi) | + |
| Kullanıcı yönetimi | – | – | + |
| Organizatör başvurusu onaylama | – | – | + |
| Uygunsuz etkinliği pasifleştirme | – | – | + |
| Kategori, şehir, mekân, salon yönetimi | – | – | + |
| Audit log inceleme | – | – | + |
| Uygunsuz yorumu kaldırma | – | – | + |

Tablodaki koyu satır önemli: "kendi etkinliği" ile "başkasının etkinliği" ayrımı rol kontrolüyle çözülemez. Karar çalışma zamanında kaynağın sahibine bakılarak verilir; bu yüzden yetkilendirme üç seviyeli kurulacak.

| Seviye | Yöntem | Örnek |
|---|---|---|
| Rol | `[Authorize(Roles = "Admin")]` | Salon silme |
| Policy | `OrganizerOnly`, `AdminOnly` | Etkinlik oluşturma |
| Kaynak | `EventOwner`, `TicketOwner`, `ReservationOwner` | Kendi etkinliğini düzenleme |

Doğrulama ölçütü: A kullanıcısının token'ıyla B kullanıcısının kaynağına istek atıldığında **403** dönmeli.

## 2. Kullanıcı hikâyeleri

**Kullanıcı**

- Bir kullanıcı olarak şehir, kategori ve tarihe göre etkinlik aramak istiyorum.
- Bir kullanıcı olarak etkinlik detayında oturum ve bilet türlerini görmek istiyorum.
- Bir kullanıcı olarak salon planı üzerinden koltuğumu seçmek istiyorum.
- Bir kullanıcı olarak seçtiğim koltukların bir süre bana ayrılmasını istiyorum.
- Bir kullanıcı olarak ödeme yapıp biletimi QR kodla almak istiyorum.
- Bir kullanıcı olarak biletimi iptal edip iade almak istiyorum.
- Bir kullanıcı olarak beğendiğim etkinlikleri favorilere eklemek istiyorum.
- Bir kullanıcı olarak katıldığım etkinliğe puan ve yorum vermek istiyorum.

**Organizatör**

- Bir organizatör olarak etkinlik oluşturup salon ve oturma planı seçmek istiyorum.
- Bir organizatör olarak bilet türleri ve fiyatları tanımlamak istiyorum.
- Bir organizatör olarak etkinliğimi yayına almak istiyorum.
- Bir organizatör olarak satış ve doluluk raporlarımı görmek istiyorum.
- Bir organizatör olarak etkinliğimi iptal edebilmek istiyorum.

**Admin**

- Bir admin olarak organizatör başvurularını onaylamak istiyorum.
- Bir admin olarak şehir, mekân, salon ve kategori yönetmek istiyorum.
- Bir admin olarak uygunsuz etkinlik ve yorumları kaldırmak istiyorum.
- Bir admin olarak sistem raporlarını ve audit logları incelemek istiyorum.

## 3. Şartnamedeki on altı soru

### 3.1 Bir etkinliğin yaşam döngüsü nasıl ilerler?

Sekiz durumlu bir makine: `Draft → PendingApproval → Published → SalesOpen → SalesClosed → Completed`. `Cancelled` ve `Suspended` her aşamadan erişilebilir. Geçişler `docs/diagrams/durum-makineleri.md` içinde çizildi.

Geçişler entity metodunda kodlanacak (`Event.Publish()`), handler'da değil. Sebep: aynı kural handler'dan da, job'dan da, testten de çağrıldığında tek yerden çalışsın.

### 3.2 Koltuk rezervasyonu nasıl yapılır?

Kullanıcı koltukları seçer, rezervasyon oluşturulur, koltuklar geçici olarak kilitlenir, ödeme tamamlanınca satışa dönüşür. Ayrıntılı akış on adım hâlinde `docs/04-eszamanlilik.md` içinde.

### 3.3 Koltuk kaç dakika kilitli tutulmalıdır?

**10 dakika.**

Gerekçe: ödeme akışını (kart bilgisi girme, 3D doğrulama benzeri adım) tamamlamaya yeter. Daha kısa süre yavaş kullanıcıyı mağdur eder; daha uzun süre popüler bir etkinlikte koltuğu gereksiz bloke eder. Süre konfigürasyondan okunacak, koda gömülmeyecek — testte 1 saniyeye indirilip süre dolumu senaryosu beklemeden doğrulanabilsin.

Kullanıcı süreyi **en fazla bir kez, 5 dakika** uzatabilir. Süresiz uzatma kilidi anlamsızlaştırır.

### 3.4 Aynı koltuğu iki kullanıcı aynı anda seçerse ne olmalıdır?

Biri başarılı olur, diğeri **409 Conflict** alır ve ekranı yenilenir. Sessiz başarısızlık kabul edilemez: her iki kullanıcı da "koltuk sizin" mesajı alırsa etkinlik gününde aynı koltuğa iki bilet çıkar.

Bu, projenin en kritik teknik problemi. Çözüm katmanlı savunma olacak — ayrıntı `docs/04-eszamanlilik.md`.

Ölçülebilir kabul kriteri: **50 paralel istek aynı koltuğu istediğinde tam olarak 1 başarı, 49 çakışma.**

### 3.5 Ödeme başarısız olduğunda rezervasyon ne olmalıdır?

Rezervasyon `Cancelled`, koltuklar anında `Available`. Koltuk boşta bekletilmez — başka kullanıcı hemen alabilmeli.

### 3.6 Etkinlik iptal edildiğinde biletler ne olmalıdır?

Tüm aktif biletler `Cancelled`, ardından iade süreci sonunda `Refunded`. Bilet sahiplerine bildirim gider. Bildirim gönderimi ödeme transaction'ının içinde **yapılmaz**; Outbox üzerinden asenkron işlenir.

### 3.7 Bilet iade politikası nasıl uygulanmalıdır?

Etkinlik tarihinden **24 saat öncesine kadar tam iade**, sonrasında iade yok. Etkinlik organizatör tarafından iptal edilirse süre şartı aranmaz, koşulsuz tam iade.

Politika metni `Event.CancellationPolicy` alanında saklanacak; etkinlik bazında değişebilmeli.

### 3.8 Kullanıcı bir oturumda en fazla kaç bilet alabilir?

**6.** Toplu alımı tamamen engellemeden karaborsayı sınırlar. Kontrol rezervasyon oluşturulurken yapılır ve kullanıcının o oturumdaki `Locked`, `PaymentPending`, `Confirmed` durumundaki tüm biletleri sayılır.

### 3.9 Hangi işlemlerde transaction gerekir?

| İşlem | Neden |
|---|---|
| Rezervasyon oluşturma | Koltuk durumu + rezervasyon + kalemler birlikte yazılmalı |
| Ödeme tamamlama | Altı işlem atomik olmalı (aşağıda) |
| İade | Ödeme durumu + bilet durumu + koltuk durumu birlikte |
| Toplu koltuk üretimi | Yarım kalmış plan bozuk veri bırakır |

Ödeme tamamlamada tek transaction içinde yapılacak altı iş: ödeme başarılı kaydı, rezervasyon onayı, bilet üretimi, koltukların satıldı işaretlenmesi, bildirim kaydı, outbox mesajı.

### 3.10 Hangi işlemlerde cache kullanılmalıdır?

Popüler etkinlikler, kategori listesi, şehir listesi, etkinlik detayı, salon oturma planı. Ortak nokta: sık okunur, seyrek değişir.

Cache'lenmeyecekler: koltuk müsaitlik durumu (saniyede değişir), kullanıcıya özel veriler (bilet, rezervasyon). Kullanıcıya özel bir veri cache'lenecekse anahtar mutlaka kullanıcı kimliği içermeli — aksi hâlde A kullanıcısı B'nin verisini görür.

**Cache kapalıyken sistem çalışmaya devam etmeli.** Redis'e erişilemezse hata loglanır ve veritabanına düşülür.

### 3.11 Hangi işlemler background job ile yapılmalıdır?

| Job | Sıklık |
|---|---|
| Süresi dolan rezervasyonları iptal etme | dakikada bir |
| Outbox mesajlarını işleme | dakikada bir |
| Başarısız mesajları yeniden deneme | 15 dakikada bir |
| Yaklaşan etkinlik hatırlatması | günde bir |
| Günlük satış özeti | günde bir |
| Rapor üretimi (export) | istek üzerine |

Ortak ilke: kullanıcının isteğini bekletmemesi gereken her iş job'a taşınır. E-posta gönderimi buna dâhil — SMTP çağrısı saniyeler sürebilir.

### 3.12 Hangi işlemler loglanmalıdır?

Giriş, başarısız giriş, etkinlik oluşturma ve yayınlama, rezervasyon oluşturma, koltuk kilitleme, ödeme, iade, background job sonuçları, cache hatası, SignalR bağlantı hatası, beklenmeyen exception.

Loglanmayacaklar: şifre (düz metin veya hash), token, kart bilgisi. E-posta ve telefon maskelenerek yazılır.

### 3.13 Hangi senaryolarda kullanıcıya bildirim gönderilmelidir?

Rezervasyon oluşturulduğunda, rezervasyon süresi dolmak üzereyken, ödeme başarılı olduğunda, ödeme başarısız olduğunda, bilet oluşturulduğunda, etkinlik tarihi yaklaştığında, etkinlik iptal edildiğinde, iade tamamlandığında, rapor hazırlandığında.

### 3.14 Hangi alanlara index eklenmelidir?

Ayrıntılı liste `docs/02-veri-modeli.md` içinde. Öne çıkanlar:

- `Events(CityId, CategoryId, EventDate)` — ana listeleme sorgusu
- `EventSeats(EventSessionId, Status)` — boş koltuk sorgusu
- `EventSeats(LockedUntil)` — süre dolumu job'ı tarayacak
- `Reservations(ExpiresAt)` — aynı job
- `OutboxMessages(CreatedAt) WHERE ProcessedAt IS NULL` — kısmi index, outbox job'ı

### 3.15 Para birimi ve tutar alanları nasıl tutulmalıdır?

`decimal(18,2)` ve ayrı bir `Currency` kolonu. `float` veya `double` **kullanılmayacak** — ikilik kayan nokta ondalık sayıyı tam temsil edemez, yüz bin biletlik satışta hata birikir.

**Toplam tutar her zaman sunucuda hesaplanır.** İstek gövdesinde tutar alanı bulunmaz; istemci yalnızca koltuk kimliklerini gönderir. Aksi hâlde tarayıcı araçlarından tutar değiştirilebilir.

### 3.16 Tarih ve saat bilgisi nasıl tutulmalıdır?

Tümü UTC ve `timestamptz`. Gösterim katmanı kullanıcının yerel saatine çevirir. Sunucuda `DateTime.Now` kullanılmayacak; zaman bir arayüz üzerinden alınacak ki testte sabitlenebilsin.

## 4. Kapsam dışı bırakılanlar

Şartnamede bulunmayan ve bilinçli olarak yapılmayacaklar:

- İndirim kuponu ve promosyon sistemi
- Çoklu para birimi ve kur dönüşümü
- Gerçek ödeme sağlayıcısı entegrasyonu (mock kullanılacak, şartname izin veriyor)
- Kart bilgisi saklama
- Kullanıcı seviyesi / puan toplama (gamification)
- Servis bedeli (tasarımda görünüyor, domain modelinde karşılığı yok — kaldırılacak)

## 5. Sonraki adım

Veri modeli kararları için `docs/02-veri-modeli.md`.
