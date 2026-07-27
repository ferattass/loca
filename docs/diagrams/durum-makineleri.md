# Durum Makineleri

> Tarih: 24.07.2026 · Gün 1

Dört varlığın yaşam döngüsü. Geçişler domain entity metotlarında kodlanacak; geçersiz bir geçiş denendiğinde `DomainException` fırlatılıp **409 Conflict** dönecek.

## Etkinlik

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> PendingApproval : organizatör gönderir
    PendingApproval --> Published : admin onaylar
    PendingApproval --> Draft : admin reddeder
    Published --> SalesOpen : satış başlangıç tarihi (job)
    SalesOpen --> SalesClosed : satış bitiş tarihi (job)
    SalesClosed --> Completed : etkinlik tarihi geçti (job)

    Draft --> Cancelled
    PendingApproval --> Cancelled
    Published --> Cancelled
    SalesOpen --> Cancelled
    SalesClosed --> Cancelled

    Published --> Suspended : admin askıya alır
    SalesOpen --> Suspended
    Suspended --> Published : admin kaldırır
    Suspended --> Cancelled

    Completed --> [*]
    Cancelled --> [*]
```

Yayına alma ön koşulları: en az bir oturum, en az bir bilet türü, poster görseli, geçerli tarih aralığı. Koşullardan biri sağlanmazsa `Publish()` çağrısı hata verir.

`Completed` durumuna gelmeden etkinliğe yorum yapılamaz.

## Rezervasyon

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Locked : koltuklar kilitlendi
    Pending --> Cancelled

    Locked --> PaymentPending : ödeme başlatıldı
    Locked --> Expired : 10 dakika doldu (job)
    Locked --> Cancelled : kullanıcı vazgeçti

    PaymentPending --> Confirmed : ödeme başarılı
    PaymentPending --> Cancelled : ödeme başarısız

    Confirmed --> Refunded : iade

    Expired --> [*]
    Cancelled --> [*]
    Refunded --> [*]
```

`Expired` ve `Cancelled` durumlarında koltuklar `Available` yapılır.

## Ödeme

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Processing : sağlayıcıya gönderildi
    Processing --> Successful : callback doğrulandı
    Processing --> Failed : sağlayıcı reddetti
    Processing --> Cancelled : kullanıcı vazgeçti
    Successful --> Refunded : iade
    Failed --> [*]
    Cancelled --> [*]
    Refunded --> [*]
```

Aynı rezervasyon için birden fazla `Successful` ödeme oluşamaz. Bu kural veritabanı seviyesinde kısmi (filtered) unique index ile garantilenir.

## Bilet

```mermaid
stateDiagram-v2
    [*] --> Active : ödeme başarılı olunca üretilir
    Active --> Used : girişte QR okutuldu
    Active --> Cancelled : kullanıcı veya etkinlik iptali
    Active --> Expired : etkinlik tarihi geçti (job)
    Cancelled --> Refunded : iade tamamlandı
    Used --> [*]
    Expired --> [*]
    Refunded --> [*]
```

Her `ReservationItem` için tam olarak bir bilet üretilir.

## Geçersiz geçişler

Bu liste birim testlerine dönüşecek. Her satır için "geçiş denendiğinde 409 döner" testi yazılacak.

| Geçersiz geçiş | Neden yasak |
|---|---|
| `Expired → PaymentPending` | Süresi dolmuş rezervasyon üzerinden ödeme başlatılamaz |
| `Cancelled → Confirmed` | İptal edilmiş rezervasyon canlandırılamaz |
| `Confirmed → Locked` | Onaylanmış rezervasyon geri kilide dönemez |
| `Refunded → Confirmed` | İade edilmiş ödeme geri alınamaz |
| `Completed → SalesOpen` | Biten etkinlik satışa açılamaz |
| `Cancelled → Published` | İptal edilmiş etkinlik yayınlanamaz |
| `Used → Active` | Kullanılmış bilet tekrar aktifleşemez |
| `Successful → Processing` | Tamamlanmış ödeme işleme dönemez |

Uygulama deseni: izin verilen geçişler entity içinde bir sözlükte tutulur, `TransitionTo()` metodu bu sözlüğe bakar. Kural tek yerde durur, okunabilir ve test edilebilir kalır.
