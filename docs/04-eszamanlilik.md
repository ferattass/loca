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
