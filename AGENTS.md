# AGENTS.md — Loca Proje Rehberi

Etkinlik, biletleme ve koltuk rezervasyon sistemi. Konser, tiyatro, konferans etkinliklerinde eşzamanlı rezervasyon, geçici koltuk kilitleme ve ödeme işlemlerini ele alır.

## Proje Özeti

| Yön | Teknoloji |
|---|---|
| **Backend** | .NET 9 · ASP.NET Core · Onion Architecture · CQRS + MediatR · EF Core · PostgreSQL · Redis · SignalR · Hangfire |
| **Frontend** | React · TypeScript · Vite · TanStack Query · Zustand · React Hook Form + Zod · Tailwind CSS |
| **Altyapı** | Docker Compose · PostgreSQL · Redis · Serilog · OpenTelemetry |

## Klasör Yapısı

```
loca/
├── src/                              Uygulama kodu
│   ├── Loca.Domain/                 Çekirdek: entity, value object, iş kuralları
│   ├── Loca.Application/            Uygulama katmanı: command, query, handler
│   ├── Loca.Infrastructure/         Dış servisler: Redis, e-posta, ödeme, Hangfire
│   ├── Loca.Persistence/            Veritabanı: EF Core, migration, repository impl.
│   └── Loca.WebApi/                 Presentation: controller, middleware, SignalR hub
├── tests/
│   ├── Loca.UnitTests/              Birim testleri
│   ├── Loca.IntegrationTests/       Entegrasyon testleri
│   ├── Loca.ArchitectureTests/      Mimari kuralları doğrulama (bağımlılık akışı)
│   └── e2e/                         Kabul testleri (Python)
├── web/                             React arayüzü
├── docs/                            Detaylı belgeler (Türkçe)
└── tools/                           Demo veri ve yönetim scriptleri
```

## Mimari Prensipler

### Onion Architecture — Bağımlılık Akışı

```
WebApi → Application → Domain ← Persistence / Infrastructure
```

**Kural:** Bağımlılık okları **hep içeri** bakar. `Domain` hiçbir şeye referans **vermez**.

| Katman | Sorumluluk | Bağımlılık |
|---|---|---|
| **Domain** | Entity, value object, iş kuralları, enum | ∅ (Framework yok) |
| **Application** | Command, query, handler, DTO, validator, arayüz tanımı | Domain |
| **Persistence** | EF Core, migration, repository impl. | Domain + Application arayüzleri |
| **Infrastructure** | Redis, e-posta, ödeme, Hangfire, Serilog | Domain + Application arayüzleri |
| **WebApi** | Controller, middleware, auth, filter, SignalR hub | Persistence, Infrastructure, Application |

**Mimarı testler** (`tests/Loca.ArchitectureTests`) bu kuralları otomatik denetler. İhlal denen bağımlılık projeler derlenmez.

### CQRS + MediatR Pipeline

```
Request → MediatR Pipeline
├── Validation (FluentValidation)
├── Authorization (Policy)
├── Logging
└── Handler (Domain işlemi)
```

Tüm HTTP istekleri MediatR `Command` veya `Query` aracılığıyla işlenir. Handler'lar stateless ve side-effect'ten arındırılmıştır.

## Geliştirme Ortamı

### 1. Kurulum

```bash
# Altyapıyı başlat
cp .env.example .env                     # Gerçek değerler doldur
docker compose up -d

# User secrets (API konteyner dışında çalışırken)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=loca;Username=loca_user;Password=<.env'deki>" \
  --project src/Loca.WebApi

dotnet user-secrets set "Jwt:Secret" "<en az 32 karakter>" --project src/Loca.WebApi

# Veritabanı
dotnet ef database update -p src/Loca.Persistence -s src/Loca.WebApi
```

### 2. API Çalıştırma

```bash
dotnet run --project src/Loca.WebApi --urls http://localhost:5000
```

| Servis | Port |
|---|---|
| API | 5000 |
| Swagger | 5000/swagger |
| pgAdmin | 5050 |
| Redis Insight | 5540 |
| Mailpit | 8025 |

### 3. Web Arayüzü

```bash
cd web
npm install
npm run dev                # http://localhost:5173
```

### 4. Testler

```bash
# Birim testleri
dotnet test tests/Loca.UnitTests

# Entegrasyon testleri
dotnet test tests/Loca.IntegrationTests

# Mimari testleri (bağımlılık akışı)
dotnet test tests/Loca.ArchitectureTests

# Kabul testleri (Python, API ayakta olmalı)
python tests/e2e/e2e_gun6.py            # Rezervasyon ve eşzamanlılık
python tests/e2e/e2e_gun7.py            # Ödeme, bilet, outbox
python tests/e2e/yetki_denetimi.py      # Yetkilendirme matrisi
```

## Önemli Kavramlar

### Eşzamanlı Koltuk Kilitleme (Kritik)

Projenin en kompleks teknik problemi: 50 paralel isteğin aynı koltuğu istediğinde **tam olarak 1 başarılı, 49 çakışma** olmalıdır.

**Üç katmanlı savunma:**
1. **Uygulama katmanı** — optimistic locking (`RowVersion`)
2. **Veritabanı** — `UNIQUE(EventSessionId, SeatId)` constraint
3. **Redis** — ön eleme (ilk seçim)

Ayrıntılar: [docs/04-eszamanlilik.md](docs/04-eszamanlilik.md)

### Durum Makineleri

Dört varlık (`Event`, `Reservation`, `Payment`, `Ticket`) durum makinesi ile modellenmiştir. **Geçişler entity metodu içinde kodlanır:**

```csharp
event.Publish()        // Draft → PendingApproval
event.Approve()        // PendingApproval → Published
reservation.Expire()   // Locked → Expired
```

Geçersiz geçiş denendiğinde `DomainException` fırlatılıp **409 Conflict** dönmektedir.

Durum makineleri: [docs/diagrams/durum-makineleri.md](docs/diagrams/durum-makineleri.md)

### Domain Olayları ve Outbox

Bir domain olayı oluştuğunda (ör. `ReservationConfirmed`):

1. Olay entity'nin `DomainEvents` listesine eklenir
2. Transaction'da **veritabanı ve outbox** atomik güncellenmiştir
3. Hangfire job'ı `OutboxMessage`'ları işleyip **observer'ları çağırır**

Bu sayede e-posta, bildirim, cache geçersizleme bir araya olmayan işlemler olarak güvenli yönetilmektedir.

### Değer Nesneleri (Value Objects)

Para, e-posta, telefon gibi primitif olmayan kavramlar değer nesnesidir:

```csharp
public record Money(decimal Amount, string Currency)
public record Email(string Value)
```

## Kodlama Standartları

### Nullable Reference Types — Zorunlu

`Directory.Build.props`'de `<Nullable>enable</Nullable>`. Tüm proje strict null checking altındadır.

**Kural:** Null referans exception'u derlemede yakalanmalı, runtime'da değil.

```csharp
string email = user.Email;           // ✅ String türü
string? maybeSth = SomeNullableFunc();    // ✅ Null olabilir
```

### Hata Yönetimi

Kullanıcı hatası → `DomainException` → **400/409**
Sistem hatası → unhandled exception → **500**

Exception middleware'i kontrolü sağlar ve anlamlı HTTP durum kodları döner.

### Loglama

Serilog tüm işlemeleri kayıt altına alır. Geliştirmede `appsettings.Development.json` üretilen SQL'i gösterir (N+1 tespiti). Production'da kapalı.

Tüm hassas veri (şifre, kredi kartı) loglanmaz.

## Ortak Görevler

### Yeni Uç Noktası Ekleme

1. **Domain:** Entity'ye metodu ekle (varsa durum değişikliği)
2. **Application:** `YapılacakCommand` ve `YapılacakHandler` yaz
3. **Persistence:** Gerekirse repository'ye metodu ekle
4. **WebApi:** Controller'a action ekle ve MediatR çağır
5. **Test:** Birim + entegrasyon + E2E (kabul)

### Veritabanı Şeması Değişikliği

```bash
# Migration oluştur
dotnet ef migrations add AdMigrationAdi -p src/Loca.Persistence

# Geliştirmede uygula
dotnet ef database update -p src/Loca.Persistence -s src/Loca.WebApi

# Production'da render.yaml deploy'ı sırasında otomatik uygulanır
```

**Kural:** Migration dosyaları elle düzenlenirse veya varsa SQL bilgisi eklenmesi gerekiyorsa — doğru kişiye sor.

### Demo Veri İşleme

Python scriptleri gerçek API'yi çağırır ve test ortamını doldurur:

```bash
python tools/demo-veri/hesaplari_doldur.py       # Demo hesapları oluştur
python tools/demo-veri/etkinlikleri_doldur.py    # Katalog doldur
python tools/demo-veri/afisleri_yenile.py        # Afis yükle (Render'da)
```

Scriptlerin avantajı: uygulamanın **kendi kurallarını** çalıştırması ve doğruluk garantisi.

## Önemli Dosyalar

| Dosya | Amaç |
|---|---|
| [README.md](README.md) | Kurulum ve teknoloji yığını |
| [YOL-HARITASI.md](YOL-HARITASI.md) | 10 günlük geliştirme planı |
| [docs/01-analiz.md](docs/01-analiz.md) | İş kuralları, roller, kullanıcı hikayeleri |
| [docs/02-veri-modeli.md](docs/02-veri-modeli.md) | 28 tablo, unique kuralları, index kararları |
| [docs/03-tasarim.md](docs/03-tasarim.md) | UI/UX ve tasarım sistemi |
| [docs/04-eszamanlilik.md](docs/04-eszamanlilik.md) | **Kritik:** Koltuk kilitleme stratejisi |
| [docs/05-odeme-iyzico.md](docs/05-odeme-iyzico.md) | İyzico entegrasyonu ve ödeme akışı |
| [docs/06-canliya-alma.md](docs/06-canliya-alma.md) | Render deployment adımları |
| [docs/diagrams/mimari.md](docs/diagrams/mimari.md) | 12 mimari diyagram |
| [src/Loca.WebApi/Program.cs](src/Loca.WebApi/Program.cs) | Dependency injection ve middleware kurulumu |

## Riskli Noktalar ve Çözümleri

| Risk | Belirt | Çözüm |
|---|---|---|
| N+1 sorgusu | Loglarda çok sayıda benzer SQL | Eager loading (.Include) veya select projection |
| Redis kapanması | Cache tuşları boş dönüş | Fallback logic; sistem çalışmaya devam etmelidir |
| SMTP sekresi çözsülememiş hale gelmesi | Posta gönderilemedi + hiç hata | User secrets kılavuzunu kontrol et; Data Protection anahtarı sıfırlanmış olabilir |
| Migration çakışması | Gemi adları aynı; migration çalışmıyor | En son migration'ı sil ve birleştir |
| Outbox job durdu | Bildirimler gitmemiş | Hangfire dashboard'da job'ı yeniden çalıştır veya `OutboxProcessing` job'ını kontrol et |
| JWT secret değişti | Eski tokenler geçersiz | Geçiş periyodu sırasında eski ve yeni secret'ı onayla (public key list) |

## Ek Kaynaklar

- **Figma Tasarımı:** https://www.figma.com/design/6UIsC0O7T9SUAoZf5dKk2R/LOCA
- **GitHub Repository:** https://github.com/ferattass/loca
- **Swagger/OpenAPI:** http://localhost:5000/swagger (geliştirmede)
- **Mimari Diyagramları:** [docs/diagrams/](docs/diagrams/)

## Ekip Notları

- **Proje dili:** Türkçe (belgeler, commit messages, kod yorumları)
- **Backend lead:** .NET mimarisi ve CQRS deseni
- **Frontend lead:** React ve tasarım sistemi tutarlılığı
- **Ödeme:** iyzico entegrasyonu — test ortamından sonra production sandbox'ıyla doğrula
- **Deployment:** Render free tier — 30 gün Postgres sınırı; uzun süreli kullanım için upgrade gerekli

---

**Son güncelleme:** Ağustos 2026 · Proje tamamlanmış ve production'da
