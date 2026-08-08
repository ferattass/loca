using Loca.Application;
using Loca.Application.Common.Interfaces;
using Loca.Infrastructure;
using Loca.Persistence;
using Loca.Persistence.Seeding;
using Loca.WebApi.Configuration;
using Loca.WebApi.Extensions;
using Loca.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Barindirma saglayicisinin (Render, Railway) verdigi DATABASE_URL /
// REDIS_URL / PORT degiskenleri uygulamanin bekledigi bicime cevriliyor.
// EN BASTA cagriliyor: sonraki her kayit bu degerleri okuyor.
BarindirmaAyarlari.Uygula(builder);

builder.SerilogKur();

// --- Servis kayitlari ---------------------------------------------------
//
// Her konu kendi kurulum dosyasinda; gerekceler oraya tasindi. Bu liste
// yalnizca "hangi konular var" sorusunu cevapliyor.
builder.Services
    .ApiSozlesmesiEkle()
    .KimlikDogrulamaEkle(builder.Configuration)
    .YetkilendirmeEkle()
    .VekilBasliklariEkle(builder.Configuration)
    .HizSinirlamaEkle(builder.Configuration)
    .TarayiciErisimiEkle(builder.Configuration)
    .SaglikKontrolleriEkle()
    .ZamanlanmisIsleriEkle(builder.Configuration);

// MediatR, pipeline davranislari ve FluentValidation dogrulayicilari.
// Katmanin ic yapisi burada bilinmez; kayit sorumlulugu katmanin kendisinde.
builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddPersistence(builder.Configuration);

// Istegi yapan kullanicinin kimligi HttpContext'ten okunur.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

// Bekleyen migration'lar uygulanir ve admin hesabi tohumlanir.
// Uclarin hicbiri acilmadan once calisir; aksi hâlde ilk istek bos
// veritabanina carpardi.
await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.IstekHattiniKur();
app.UclariBagla(builder.Configuration);

app.Run();

// Integration testlerde WebApplicationFactory<Program> kullanilabilmesi icin gerekli.
public partial class Program;
