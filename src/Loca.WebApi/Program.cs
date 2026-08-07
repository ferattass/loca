using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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
using Loca.WebApi.Configuration;
using Loca.WebApi.BackgroundJobs;
using Loca.WebApi.HealthChecks;
using Loca.WebApi.Middleware;
using Loca.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Barindirma saglayicisinin (Render, Railway) verdigi DATABASE_URL /
// REDIS_URL / PORT degiskenleri uygulamanin bekledigi bicime cevriliyor.
// EN BASTA cagriliyor: sonraki her kayit bu degerleri okuyor.
BarindirmaAyarlari.Uygula(builder);

// --- Loglama ------------------------------------------------------------
//
// Serilog paketi bastan beri referansliydi ama hic baglanmamisti; loglar
// varsayilan konsol saglayicisindan cikiyordu. Yapilandirilmis loglamanin
// farki, satirin metin degil ALAN tasimasi: "OdemeId: 019f..." bir metin
// parcasi degil sorgulanabilir bir alan oluyor ve bir odemenin butun izi
// tek bir filtreyle toplanabiliyor.
builder.Host.UseSerilog((context, services, yapilandirma) => yapilandirma
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    // Makine adi cok ornekli calisirken "hangi sunucuda" sorusunu
    // cevapliyor; tek sunucuda zararsiz bir alan.
    .Enrich.WithProperty("Uygulama", "Loca.WebApi"));

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
    // Admin de dahil: onay yetkisi admin'in zaten sahip oldugu yetkilerin
    // alt kumesi ve haric tutulsaydi tek admin hesabiyla kurulan bir
    // sistemde onay kuyrugu hic acilamazdi.
    .AddPolicy(Policies.ModeratorOnly, policy =>
        policy.RequireRole(RoleNames.Moderator, RoleNames.Admin))
    .AddPolicy(Policies.ResourceOwner, policy =>
        policy.AddRequirements(new ResourceOwnerRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();

// Hata yanitlari RFC 7807 Problem Details formatinda doner.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

        // Izleme kimligi hata govdesine de konuyor: kullanici destege
        // ekrandaki degeri soyleyebilsin ve o deger dogrudan log
        // satirlariyla eslessin. traceId yalnizca tek sunucu icinde
        // anlamli, bu ise istegin butun zincirini kapsiyor.
        if (ctx.HttpContext.Items.TryGetValue(
                CorrelationIdMiddleware.BaslikAdi, out var kimlik) && kimlik is string metin)
        {
            ctx.ProblemDetails.Extensions["correlationId"] = metin;
        }
    };
});

// --- Vekil sunucu arkasi ------------------------------------------------
//
// Uretimde uygulama bir ters vekilin (nginx, yuk dengeleyici, PaaS yonlendirici)
// arkasinda duruyor. O durumda RemoteIpAddress vekilin adresi oluyor ve
// hiz sinirlamasi butun kullanicilari tek bir istemci sayardi; odeme
// saglayicisina giden alici IP'si de yanlis olurdu.
//
// GUVENILEN VEKIL LISTESI ZORUNLU. Liste bos birakilip basliklar
// kayitsizca okunsaydi, X-Forwarded-For'u ISTEMCI de gonderebildigi icin
// herkes kendi IP'sini uydurabilir ve hiz sinirlamasini her istekte farkli
// bir adres yazarak tamamen atlatabilirdi.
var guvenilenVekiller = builder.Configuration
    .GetSection("App:TrustedProxies").Get<string[]>() ?? [];

builder.Services.Configure<ForwardedHeadersOptions>(secenekler =>
{
    secenekler.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Varsayilan olarak localhost guvenilir sayiliyor; onu da kaldirip
    // yalnizca acikca yazilan adresleri kabul ediyoruz.
    secenekler.KnownNetworks.Clear();
    secenekler.KnownProxies.Clear();

    foreach (var vekil in guvenilenVekiller)
    {
        if (IPAddress.TryParse(vekil, out var adres))
            secenekler.KnownProxies.Add(adres);
    }
});

// --- Hiz sinirlamasi ----------------------------------------------------
//
// Asil hedef kimlik uclari: sifre denemesi, kayit ve sifre sifirlama
// istegi. Bu uclar olmadan bir sozluk saldirisi saniyede yuzlerce sifre
// deneyebiliyor ve BCrypt'in yavasligi bu durumda savunma degil, sunucuyu
// tuketen bir maliyet hâline geliyor.
//
// Bolutleme IP'ye gore: kimlik dogrulanmamis uclarda kullanici kimligi
// yok. IP paylasiliyor olabilir (kurumsal ag, mobil operator), bu yuzden
// esikler tek bir insanin yapacagindan cok daha yukseklerde.
// Degerler yapilandirmadan: uretim degerleri uctan uca betikleri kiriyor
// (yaris testi tek IP'den 50 kullanici kaydediyor). Gelistirmede genis,
// guvenlik betigi ise API'yi dar degerlerle kaldirip siniri gercekten
// asiyor — sinirlama boylece hem denenmis oluyor hem de digerlerini
// engellemiyor.
var hizSinirlari = builder.Configuration
    .GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddRateLimiter(secenekler =>
{
    secenekler.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Istemci ne zaman tekrar deneyecegini bilmeli; bu baslik olmadan
    // dogru davranan bir istemci bile korlemesine yeniden dener.
    secenekler.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var sure))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)sure.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/problem+json";

        await context.HttpContext.Response.WriteAsync(
            """{"title":"Cok fazla istek","status":429,"detail":"Kisa surede cok fazla deneme yapildi. Biraz bekleyip tekrar deneyin.","code":"TooManyRequests"}""",
            cancellationToken);
    };

    secenekler.AddPolicy(RateLimitPolicies.Auth, context =>
        SabitPencere(IstemciAnahtari(context), hizSinirlari.Auth));

    // Sifre sifirlama daha dar: her istek bir e-posta gonderiyor ve bu
    // uc, baskasinin kutusuna posta yagdirmak icin kullanilabilir.
    secenekler.AddPolicy(RateLimitPolicies.PasswordReset, context =>
        SabitPencere(IstemciAnahtari(context), hizSinirlari.PasswordReset));

    // Genel sinir yalnizca kaba bir tavan: normal kullanimda goze
    // carpmayacak kadar yuksek, tek bir istemcinin sunucuyu tuketmesini
    // engelleyecek kadar dusuk.
    //
    // Saglik uclari DISARIDA: yonlendirici saniyede birkac kez soruyor ve
    // sinira takilsaydi saglikli bir sunucu olu sayilirdi.
    secenekler.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/health")
            ? RateLimitPartition.GetNoLimiter("saglik")
            : SabitPencere(IstemciAnahtari(context), hizSinirlari.Global));
});

static RateLimitPartition<string> SabitPencere(string anahtar, RateLimitKurali kural) =>
    RateLimitPartition.GetFixedWindowLimiter(
        anahtar,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = kural.PermitLimit,
            Window = kural.Window,
            // Kuyruk YOK: bekletilen bir giris istegi, saldirgan icin
            // ucretsiz bir yavaslatma araci olurdu.
            QueueLimit = 0
        });

// Kimlik dogrulanmissa kullanici, degilse IP. Oturum acmis bir kullanici
// IP'sini degistirerek siniri sifirlayamasin diye kimlik once geliyor.
static string IstemciAnahtari(HttpContext context) =>
    context.User.Identity?.IsAuthenticated == true
        ? $"u:{context.User.FindFirst(ClaimNames.Subject)?.Value ?? context.User.Identity.Name}"
        : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor"}";

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

// Iki ayri soru iki ayri uc: "surec ayakta mi" (liveness) ve "istek
// alabilir mi" (readiness). Tek uc olsaydi, veritabani birkac saniye
// yanit vermediginde yonlendirici surecin kendisini olu sayip yeniden
// baslatirdi — oysa yapilmasi gereken tek sey trafigi kesmek.
builder.Services.AddHealthChecks()
    .AddCheck<VeritabaniSaglikKontrolu>("veritabani", tags: ["hazir"])
    .AddCheck<RedisSaglikKontrolu>("redis", tags: ["hazir"]);

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

// Sure dolumu turunun sikligi yapilandirmadan. Bu deger Gun 7'den beri
// OLU idi: is Hangfire'a tasininca sabit Cron.Minutely yazilmis, ayar
// appsettings'te ve kabul betiklerinin belgesinde durmaya devam etmisti.
var sureDolumuSaniye = builder.Configuration
    .GetValue<int?>("Reservation:ExpirySweepSeconds") ?? 30;

// Islerin cogu veritabani islemi; is parcacigi sayisi cekirdek sayisiyla
// sinirlaniyor, varsayilan (cekirdek x 5) baglanti havuzunu tuketebilir.
builder.Services.AddHangfireServer(secenekler =>
{
    secenekler.WorkerCount = Math.Max(2, Environment.ProcessorCount);

    // Cron tek basina yetmiyor: Hangfire zamanlanmis isleri kendi yoklama
    // araliginda ariyor ve varsayilan aralik 15 saniye. Bes saniyelik bir
    // cron yazilip buraya dokunulmasaydi is yine on bes saniyede bir
    // kosardi — ayar gorunurde isler, gercekte islemezdi.
    if (sureDolumuSaniye is > 0 and < 15)
        secenekler.SchedulePollingInterval = TimeSpan.FromSeconds(sureDolumuSaniye);
});

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

// SIRA: en dista vekil basliklari. Sonraki her sey (loglama, hiz
// sinirlamasi, kimlik) istemcinin gercek adresini gormeli.
app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Istek loglamasi izleme kimliginden SONRA: boylece "GET /x 200" satiri
// da ayni kimlikle etiketleniyor ve istegin ozeti ile ayrintisi bir arada
// okunuyor.
app.UseSerilogRequestLogging(secenekler =>
{
    // Varsayilan sablon yolu ve sureyi yaziyor; kim ve nereden bilgisi
    // bir arizayi anlamak icin cogu zaman sart.
    secenekler.EnrichDiagnosticContext = (baglam, http) =>
    {
        baglam.Set("Ip", http.Connection.RemoteIpAddress?.ToString());

        if (http.User.Identity?.IsAuthenticated == true)
            baglam.Set("KullaniciId", http.User.FindFirst(ClaimNames.Subject)?.Value);
    };

    // Saglik kontrolleri saniyede birkac kez geliyor; Information
    // seviyesinde yazilsalardi log'un tamamini kaplar ve gercek
    // istekleri gorunmez hâle getirirlerdi.
    secenekler.GetLevel = (http, sure, hata) =>
        hata is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
});

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

// Hiz sinirlamasi kimlikten SONRA: bolutleme once kullaniciya, kimlik
// yoksa IP'ye bakiyor ve kullaniciyi ancak kimlik dogrulandiktan sonra
// bilebiliyoruz.
app.UseRateLimiter();

// Surec ayakta mi — bagimliliklara BAKMIYOR. Buraya bir veritabani
// kontrolu konsaydi, veritabani birkac saniye yanit vermediginde
// yonlendirici surecin kendisini olu sayip yeniden baslatirdi.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

// Istek alabilir mi — bagimliliklara BAKIYOR.
app.MapHealthChecks("/health/hazir", new HealthCheckOptions
{
    Predicate = kontrol => kontrol.Tags.Contains("hazir"),
    ResponseWriter = async (context, rapor) =>
    {
        context.Response.ContentType = "application/json";

        // Yalnizca ad ve durum. Istisna metni ve sure DISARI VERILMIYOR:
        // bu uc kimlik dogrulamasiz ve hata metinleri baglanti dizesi,
        // sunucu adi gibi seyler tasiyabiliyor.
        var govde = new
        {
            durum = rapor.Status.ToString(),
            kontroller = rapor.Entries.Select(g => new
            {
                ad = g.Key,
                durum = g.Value.Status.ToString()
            })
        };

        await context.Response.WriteAsJsonAsync(govde);
    }
});

app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", service = "Loca API" }))
   .WithName("Ping")
   .WithTags("Sistem");

// Hangfire panosu artik uretimde de acik ama YALNIZCA Admin rolune.
// Kapali birakmak guvenliydi, isleri gormenin tek yolunu da kapatiyordu:
// uretimde bir is takildiginda elde yalnizca outbox tablosu kalirdi.
// Filtre olmadan acilsaydi is govdeleri (kisisel veri tasiyorlar) ve hata
// ayrintilari kimlik dogrulamasi olmadan gorulurdu.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfirePanoFiltresi()]
});

// Tekrarlayan isler her acilista AddOrUpdate ile yeniden yaziliyor: sabit
// kimlik kullanildigi icin kopyalanmiyor, yalnizca guncelleniyor. Kodda
// degisen bir siklik boylece deploy ile birlikte etkili oluyor.
ZamanlanmisIsKaydi.TekrarlayanIsleriKaydet(
    app.Services.GetRequiredService<IRecurringJobManager>(), sureDolumuSaniye);

app.MapControllers();

// --- Tek servis dagitimi ----------------------------------------------------
//
// Arayuz derlemesi wwwroot'a kopyalanmissa API onu da servis ediyor.
// Ucretsiz barindirma katmanlarinda servis sayisi sinirli ve iki ayri
// servis calistirmak hem ikinci bir uyanma suresi hem de CORS ve
// iyzico callback'i icin ikinci bir alan adi demek. Tek kokten servis
// edildiginde arayuz ile API ayni origin'de oluyor: CORS devre disi
// kaliyor ve callback adresi tahmin edilebilir hâle geliyor.
//
// Klasor yoksa (yerel gelistirme, konteyner yigini) bu blok hic
// calismiyor ve arayuz eskisi gibi Vite'tan geliyor.
var arayuzKlasoru = Path.Combine(app.Environment.ContentRootPath, "wwwroot");

if (Directory.Exists(arayuzKlasoru) &&
    File.Exists(Path.Combine(arayuzKlasoru, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // SPA derin yollari (/biletlerim, /yonetim/odemeler) sunucuda karsiligi
    // olmayan adresler; index.html donmezse dogrudan acilan her baglanti
    // 404 alirdi. API ve saglik yollari HARIC tutuluyor, aksi hâlde
    // tanimsiz bir API ucu JSON hatasi yerine HTML donerdi ve istemci
    // "beklenmeyen token <" diye anlasilmaz bir hata gorurdu.
    app.MapFallback(async context =>
    {
        var yol = context.Request.Path.Value ?? string.Empty;

        if (yol.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            yol.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            yol.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase) ||
            yol.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(arayuzKlasoru, "index.html"));
    });
}

app.Run();

// Integration testlerde WebApplicationFactory<Program> kullanilabilmesi icin gerekli.
public partial class Program;
