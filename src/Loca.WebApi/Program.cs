var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(WebCors);

// Surec ayakta mi. Veritabani ve Redis kontrolleri Gun 9'da eklenecek.
app.MapHealthChecks("/health");

app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", service = "Loca API" }))
   .WithName("Ping")
   .WithTags("Sistem");

app.MapControllers();

app.Run();

// Integration testlerde WebApplicationFactory<Program> kullanilabilmesi icin gerekli.
public partial class Program;
