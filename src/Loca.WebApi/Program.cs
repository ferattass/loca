using System.Text;
using System.Text.Json.Serialization;
using Loca.Application;
using Loca.Application.Common.Authentication;
using Loca.Application.Common.Interfaces;
using Loca.Domain.Constants;
using Loca.Infrastructure;
using Loca.Infrastructure.Authentication;
using Hangfire;
using Hangfire.PostgreSql;
using Loca.Persistence;
using Loca.Persistence.Seeding;
using Loca.WebApi.Authorization;
using Loca.WebApi.BackgroundJobs;
using Loca.WebApi.Middleware;
using Loca.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enum'lar sayi degil METIN olarak tasinir: yanitta "Available"
        // gorunur, 1 gorunmez. Sayi tasinsaydi istemci sihirli sabitlerle
        // karsilastirma yapmak zorunda kalir ve enum'a araya yeni bir deger
        // eklendiginde arayuz sessizce yanlis durumu gosterirdi.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR, pipeline davranislari ve FluentValidation dogrulayicilari.
// Katmanin ic yapisi burada bilinmez; kayit sorumlulugu katmanin kendisinde.
builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddPersistence(builder.Configuration);

// Istegi yapan kullanicinin kimligi HttpContext'ten okunur.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// --- Kimlik dogrulama ---------------------------------------------------

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt yapilandirmasi bulunamadi.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claim adlari oldugu gibi okunur. Varsayilan esleme "sub" claim'ini
        // uzun bir URI'ye cevirir ve token'i ureten tarafla okuyan taraf
        // birbirini bulamaz.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),

            ValidateLifetime = true,

            // Varsayilan tolerans 5 dakika. 15 dakikalik bir access token'da
            // bu, omrun ucte birini uzatmak demek — sifira cekildi.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role
        };
    });

// --- Yetkilendirme ------------------------------------------------------

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
    .AddPolicy(Policies.OrganizerOnly, policy =>
        policy.RequireRole(RoleNames.Organizer, RoleNames.Admin))
    .AddPolicy(Policies.ResourceOwner, policy =>
        policy.AddRequirements(new ResourceOwnerRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();

// Hata yanitlari RFC 7807 Problem Details formatinda doner.
// Correlation ID Gun 9'da eklenecek; simdilik istek yolu ve izleme kimligi yeterli.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

// Arayuz ayri portta calistigi icin gerekli. Uretimde acik uclu birakilmayacak.
const string WebCors = "web";
builder.Services.AddCors(options =>
    options.AddPolicy(WebCors, policy => policy
        .WithOrigins(builder.Configuration["App:WebUrl"] ?? "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Yakalanmamis hatalari durum koduna cevirir. UseExceptionHandler hattina takilir.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks();

// --- Zamanlanmis isler (Hangfire) ---------------------------------------
//
// Gun 6'da basit bir BackgroundService vardi; sure dolumu yalnizca uygulama
// ayaktayken calisiyordu ve bir turun basarisiz olup olmadigi gorunmuyordu.
// Hangfire isleri veritabaninda tutuyor: uygulama yeniden baslasa da kaldigi
// yerden devam ediyor, basarisiz is yeniden deneniyor ve panelden
// gozlemlenebiliyor. Isin kendisi Application katmaninda durdugu icin bu
// gecis yalnizca tetikleyiciyi degistirdi.
var hangfireBaglanti = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddHangfire(yapilandirma => yapilandirma
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(secenekler => secenekler.UseNpgsqlConnection(hangfireBaglanti)));

// Islerin cogu veritabani islemi; is parcacigi sayisi cekirdek sayisiyla
// sinirlaniyor, varsayilan (cekirdek x 5) baglanti havuzunu tuketebilir.
builder.Services.AddHangfireServer(secenekler =>
    secenekler.WorkerCount = Math.Max(2, Environment.ProcessorCount));

builder.Services.AddScoped<ZamanlanmisIsler>();

var app = builder.Build();

// Bekleyen migration'lar uygulanir ve admin hesabi tohumlanir.
// Uclarin hicbiri acilmadan once calisir; aksi hâlde ilk istek bos
// veritabanina carpardi.
await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(WebCors);

// Sira onemli: once "kimsin" (authentication), sonra "iznin var mi"
// (authorization). Ters cevrilirse yetkilendirme heniz kimlik olusmadan calisir.
app.UseAuthentication();
app.UseAuthorization();

// Surec ayakta mi. Veritabani ve Redis kontrolleri Gun 9'da eklenecek.
app.MapHealthChecks("/health");

app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", service = "Loca API" }))
   .WithName("Ping")
   .WithTags("Sistem");

// Hangfire panosu YALNIZCA gelistirmede acik. Uretimde acik birakilsaydi
// is govdeleri ve hata ayrintilari kimlik dogrulamasi olmadan gorulurdu;
// yetkilendirme filtresi Gun 9'un guvenlik isiyle birlikte gelecek.
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

// Tekrarlayan isler her acilista AddOrUpdate ile yeniden yaziliyor: sabit
// kimlik kullanildigi icin kopyalanmiyor, yalnizca guncelleniyor. Kodda
// degisen bir siklik boylece deploy ile birlikte etkili oluyor.
ZamanlanmisIsKaydi.TekrarlayanIsleriKaydet(
    app.Services.GetRequiredService<IRecurringJobManager>());

app.MapControllers();

app.Run();

// Integration testlerde WebApplicationFactory<Program> kullanilabilmesi icin gerekli.
public partial class Program;
