# Veri Modeli

> Tarih: 24.07.2026 · Gün 1

Yirmi sekiz tablo, sekiz mantıksal grup. Bu belge kolon listelerini değil **kararları** kayıt altına alır; kolonların kendisi EF Core konfigürasyon sınıflarında yaşayacak.

## 1. Genel kararlar

| Konu | Karar | Gerekçe |
|---|---|---|
| Birincil anahtar | `Guid` (v7) | Zaman sıralı GUID, rastgele GUID'in aksine index parçalanmasına yol açmaz |
| Para | `decimal(18,2)` + `Currency` | `float` ondalık sayıyı tam temsil edemez |
| Tarih | UTC, `timestamptz` | Saat dilimi karmaşasını gösterim katmanına bırakır |
| Enum | Veritabanında `int`, API'de string | Index performansı + okunabilir API |
| Audit | `CreatedAt/By`, `UpdatedAt/By` | Interceptor ile otomatik doldurulacak |
| Soft delete | `IsDeleted`, `DeletedAt` | Global query filter ile sorgulardan düşer |

## 2. Tablo grupları

| Grup | Tablolar |
|---|---|
| Kimlik | Users, Roles, UserRoles, RefreshTokens |
| Organizatör | OrganizerProfiles, OrganizerApplications |
| Mekân | Cities, Venues, Halls, SeatLayouts, SeatSections, Seats |
| Etkinlik | Events, EventCategories, EventSessions, TicketTypes, EventSeats |
| Satış | Reservations, ReservationItems, Payments, PaymentTransactions |
| Bilet | Tickets, TicketQrCodes |
| Sosyal | Favorites, Reviews |
| Sistem | Notifications, AuditLogs, OutboxMessages, UploadedFiles |

## 3. En kritik karar: `Seats` ve `EventSeats` ayrımı

Bu ayrım yanlış kurulursa eş zamanlılık çözümü çöker.

**`Seats`** salonun fiziksel koltuğudur. Bölüm, sıra ve numaradan oluşur. Etkinlikten bağımsızdır, kalıcıdır. Üzerinde fiyat, satış durumu veya kilit bilgisi **bulunmaz**. Yalnızca `IsActive` alanı değişir (bozuk koltuk, kolon arkası).

**`EventSeats`** o koltuğun belirli bir oturumdaki satılabilir hâlidir.

```
EventSeat
├── EventSessionId
├── SeatId
├── TicketTypeId
├── Price, Currency        ← üretim anında TicketType'tan kopyalanır
├── Status                 ← Available | Locked | Reserved | Sold | Disabled
├── LockedUntil?
├── LockedByUserId?
├── ReservationId?
└── RowVersion             ← concurrency token
```

Aynı fiziksel A-1 koltuğu, üç farklı oturumda üç ayrı `EventSeat` satırına sahip olur; her birinin kendi durumu ve kendi fiyatı vardır.

**Fiyat neden kopyalanıyor?** Bilet türünün fiyatı sonradan değişirse, o koltuğu daha önce satın almış kişinin ödediği tutar değişmemeli. Referans tutulsaydı geçmiş satışların tutarı da değişirdi.

`EventSeats` satırları etkinlik **yayına alınırken** toplu üretilir.

## 4. Unique kurallar

Şartnamenin istediği beş kural ve tasarım sırasında eklenen üçü:

| # | Kural | Constraint |
|:---:|---|---|
| 1 | Aynı oturumda aynı koltuk bir kez | `UNIQUE(EventSessionId, SeatId)` |
| 2 | Bilet numarası benzersiz | `UNIQUE(TicketNumber)` |
| 3 | QR kod değeri benzersiz | `UNIQUE(QrCodeValue)` |
| 4 | Bir kullanıcı bir etkinliği bir kez favoriler | composite PK `(UserId, EventId)` |
| 5 | Bir kullanıcı bir etkinliğe bir yorum | `UNIQUE(UserId, EventId)` |
| 6 | Aynı rezervasyonda aynı koltuk iki kez olamaz | `UNIQUE(ReservationId, EventSeatId)` |
| 7 | Aynı rezervasyona tek başarılı ödeme | kısmi unique, `WHERE Status = Successful` |
| 8 | Aynı bölümde sıra + koltuk no tekrar edemez | `UNIQUE(SeatSectionId, RowLabel, SeatNumber)` |

**Neden constraint, kod kontrolü değil?** Uygulamadaki `if` kontrolü iki eşzamanlı istekte ikisi de geçebilir. Veritabanı constraint'i atomiktir. Kod kontrolü kullanıcı deneyimi için (anlamlı hata mesajı), constraint doğruluk için. İkisi birlikte gerekir.

1 numaralı kural özellikle önemli: yarış durumunun son savunma hattı bu tek satırdır.

## 5. Index kararları

| Tablo | Index | Hangi sorgu |
|---|---|---|
| Users | `Email` UNIQUE | giriş |
| RefreshTokens | `Token` UNIQUE, `UserId` | token yenileme |
| Events | `(CityId, CategoryId, EventDate)` | ana listeleme |
| Events | `Title` | arama |
| Events | `OrganizerId` | organizatör paneli |
| EventSessions | `(HallId, StartDate, EndDate)` | salon çakışma kontrolü |
| **EventSeats** | `(EventSessionId, Status)` | boş koltuk listesi |
| **EventSeats** | `LockedUntil` (kısmi, `WHERE Status = Locked`) | süre dolumu job'ı |
| Reservations | `(UserId, Status)` | biletlerim |
| **Reservations** | `ExpiresAt` | süre dolumu job'ı |
| Payments | `ReservationId` | ödeme sorgusu |
| Tickets | `UserId`, `TicketNumber` UNIQUE | biletlerim, bilet arama |
| Notifications | `(UserId, IsRead)` | okunmamış sayacı |
| **OutboxMessages** | `CreatedAt` (kısmi, `WHERE ProcessedAt IS NULL`) | outbox job'ı |
| AuditLogs | `(EntityName, EntityId)`, `OccurredAt` | denetim sorgusu |

**Kısmi index neden?** `OutboxMessages` tablosunda bir milyon işlenmiş kayıt birikse bile, job yalnızca işlenmemişleri arar. Kısmi index sadece o alt kümeyi tutar; index küçük kalır, tarama hızlı olur.

## 6. Cascade davranışları

| İlişki | Davranış | Gerekçe |
|---|---|---|
| User → UserRoles | Cascade | Kullanıcı silinirse rol ataması anlamsız |
| User → RefreshTokens | Cascade | Aynı |
| Event → EventSessions | Cascade | Oturum etkinliğe bağımlı |
| EventSession → EventSeats | Cascade | Koltuk oturuma bağımlı |
| Reservation → ReservationItems | Cascade | Kalem rezervasyona bağımlı |
| Payment → PaymentTransactions | Cascade | İşlem kaydı ödemeye bağımlı |
| **Venue → Events** | **Restrict** | Etkinliği olan mekân silinemez |
| **Hall → EventSessions** | **Restrict** | Aynı |
| **Seat → EventSeats** | **Restrict** | Satılmış koltuğun kaydı korunmalı |
| **User → Reservations** | **Restrict** | Satış geçmişi silinemez |

Genel ilke: **para ve satış geçmişi içeren hiçbir zincir cascade değildir.** Muhasebe kaydı kullanıcı silindi diye kaybolmamalı.

## 7. Soft delete kullanılacak tablolar

`Events`, `Venues`, `Halls`, `SeatLayouts`.

Ortak nokta: geçmiş satış kayıtları bunlara referans veriyor. Fiziksel silme yabancı anahtar zincirini kırar veya raporları bozar.

Şartname "kullanılmış oturma planı fiziksel olarak silinmemelidir" diyor; bu kural soft delete ile karşılanıyor.

## 8. Concurrency token kullanılacak tablolar

`EventSeats` ve `Reservations`.

PostgreSQL'de `xmin` sistem kolonu concurrency token olarak kullanılabiliyor; ayrı bir `RowVersion` kolonu açmaya gerek yok. Satır okunduktan sonra başkası tarafından değiştirilmişse `SaveChanges` hata verir ve istek 409 döner.

## 9. Sonraki adım

Eşzamanlılık stratejisi kararı: `docs/04-eszamanlilik.md`.
