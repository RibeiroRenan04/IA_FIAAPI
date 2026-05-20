using SimplificadorLinguagem.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Porta dinâmica (Render injeta a variável PORT) ──────────────────────────
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(renderPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");

// ── OpenAI HTTP Client ────────────────────────────────────────────────────────
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout     = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<OpenAIService>();

// ── Upload: limite de 10 MB ───────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title       = "Simplificador de Linguagem API",
        Version     = "v1",
        Description = "Transforma textos jurídicos, médicos e governamentais em linguagem simples."
    }));

var app = builder.Build();

// ── Swagger (disponível em todos os ambientes) ────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

// CORS deve vir antes dos controllers
app.UseCors("Angular");

// ── Endpoints utilitários ─────────────────────────────────────────────────────
app.MapGet("/",       () => "Simplificador de Linguagem API ONLINE");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapControllers();
app.Run();
