# Loca — Etkinlik, Biletleme ve Koltuk Rezervasyon Sistemi

Konser, tiyatro ve konferans etkinliklerinin oluşturulduğu; kullanıcıların görsel koltuk planından koltuk seçerek rezervasyon yaptığı ve ödeme akışıyla bilet aldığı web tabanlı sistem.

Bu proje yalnızca CRUD değildir. Ele alınan asıl problemler: eş zamanlı rezervasyon, geçici koltuk kilitleme, kapasite kontrolü, ödeme sonucu işleme, bildirim, loglama, cache ve background job.

## Teknoloji

**Backend** — .NET 9 · ASP.NET Core Web API · Onion Architecture · CQRS + MediatR · EF Core · PostgreSQL · Redis · SignalR · Hangfire · Serilog · OpenTelemetry

**Frontend** — React · TypeScript · Vite · TanStack Query · Zustand · React Hook Form + Zod · Tailwind CSS

**Altyapı** — Docker Compose (PostgreSQL, Redis, pgAdmin, Redis Insight, Mailpit)

## Yapı

```
loca/
├── src/
│   ├── Loca.Domain/           entity, value object, domain kuralları
│   ├── Loca.Application/      command, query, handler, validator
│   ├── Loca.Infrastructure/   redis, e-posta, storage, job, ödeme
│   ├── Loca.Persistence/      EF Core, migration, repository
│   └── Loca.WebApi/           controller, middleware, hub
├── tests/
│   ├── Loca.UnitTests/
│   ├── Loca.IntegrationTests/
│   └── Loca.ArchitectureTests/
├── web/                       React arayüzü
├── docs/                      analiz, veri modeli, kararlar
└── docker-compose.yml
```

## Kurulum

### 1. Altyapı

```bash
cp .env.example .env      # gerçek değerleri doldur
docker compose up -d      # postgres, redis, pgadmin, redis insight, mailpit
```

### 2. Yerel geliştirme sırları

API konteyner dışında çalıştırılırken bağlantı dizesi ve JWT anahtarı **user-secrets**'tan okunur. Bunlar depoya girmez:

```bash
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=loca;Username=loca_user;Password=<.env'deki değer>" \
  --project src/Loca.WebApi

dotnet user-secrets set "Jwt:Secret" "<en az 32 karakter>" --project src/Loca.WebApi
```

Konteynerde çalışırken aynı değerler `ConnectionStrings__Default` ve `Jwt__Secret` ortam değişkenlerinden gelir; `docker-compose.yml` bunları `.env`'den aktarır.

### 3. Veritabanı şeması

```bash
dotnet ef database update -p src/Loca.Persistence -s src/Loca.WebApi
```

Roller (`Customer`, `Organizer`, `Admin`) migration ile tohumlanır.

### 4. Çalıştırma

```bash
dotnet run --project src/Loca.WebApi --urls http://localhost:5000
cd web && npm run dev
```

| Servis | Adres |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Web | http://localhost:5173 |
| pgAdmin | http://localhost:5050 |
| Redis Insight | http://localhost:5540 |
| Mailpit | http://localhost:8025 |

## Dokümantasyon

- [İş analizi](docs/01-analiz.md) — roller, iş kuralları, kararlar
- [Veri modeli](docs/02-veri-modeli.md) — 28 tablo, unique ve index kararları
- [Tasarım](docs/03-tasarim.md) — Figma, tasarım sistemi, ekranlar
- [Eşzamanlılık kararı](docs/04-eszamanlilik.md) — koltuk kilitleme stratejisi
- [Mimari](docs/diagrams/mimari.md) — 12 diyagram: katmanlar, istek akışı, kimlik doğrulama, rezervasyon
- [Durum makineleri](docs/diagrams/durum-makineleri.md) — etkinlik, rezervasyon, ödeme, bilet

## Bağlantılar

- Tasarım: [Figma — LOCA](https://www.figma.com/design/6UIsC0O7T9SUAoZf5dKk2R/LOCA)
- Depo: https://github.com/ferattass/loca
