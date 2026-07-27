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

```bash
cp .env.example .env
docker compose up -d
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

## Bağlantılar

- Tasarım: [Figma — LOCA](https://www.figma.com/design/6UIsC0O7T9SUAoZf5dKk2R/LOCA)
- Depo: https://github.com/ferattass/loca
