using System.Threading.RateLimiting;
using DotNetEnv;
using KycLite.Api.Controllers;
using KycLite.Api.Extraction;
using KycLite.Api.Infrastructure;
using KycLite.Api.Services;
using KycLite.Api.Validation;
using KycLite.Api.Validation.FieldRules;
using Microsoft.OpenApi;

// Load .env (searching up the directory tree) into environment variables before configuration
// is built, so DocumentIntelligence__* keys flow into IConfiguration.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "frontend";
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Open demo with no auth/credentials: every origin is allowed by design so the app
        // works from any dev port or a hosted frontend. Tighten this if auth is ever added.
        .SetIsOriginAllowed(_ => true)));

builder.Services.AddControllers();

// The .NET clock abstraction: services depend on this instead of DateTime.UtcNow, so date-driven
// behaviour is deterministically testable (a FakeTimeProvider stands in for the wall clock).
builder.Services.AddSingleton(TimeProvider.System);

// Guard the verify endpoint (each real call can incur a billed Azure transaction) with a per-client
// fixed window, so a single caller can't drive cost or starve others. The limit is aligned with the
// Azure Document Intelligence free (F0) tier, which caps at ~20 requests/minute — so the demo can't
// out-run the free quota and get throttled by Azure itself. Raise this for a paid (S0) tier.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(VerificationController.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Consistent RFC 7807 error responses: unhandled exceptions become ProblemDetails 500s
// (no leaked stack traces) rather than the raw developer exception page.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Liveness probe for containers/uptime monitoring.
builder.Services.AddHealthChecks();

// Interactive API docs (Swagger UI at /swagger). The discovery-driven endpoints are the
// centrepiece of this demo, so they're worth exploring in the browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KYC-Lite API",
        Version = "v1",
        Description = "ID/passport verification demo: extract document fields and run "
            + "user-composed field checks for an approve/reject verdict.",
    });

    // Fold in the XML summaries emitted by GenerateDocumentationFile, when present.
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "KycLite.Api.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// --- Extraction provider: Azure when configured, otherwise the offline mock. ---
builder.Services
    .AddOptions<DocumentIntelligenceOptions>()
    .Bind(builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName))
    // When Azure is configured, fail fast at startup on a malformed endpoint rather than
    // throwing on the first request. In mock mode (nothing set) the predicate is a no-op.
    .Validate(
        o => !o.IsConfigured || Uri.TryCreate(o.Endpoint, UriKind.Absolute, out _),
        "DocumentIntelligence__Endpoint must be a valid absolute URI when Azure credentials are set.")
    .ValidateOnStart();

var diOptions = builder.Configuration
    .GetSection(DocumentIntelligenceOptions.SectionName)
    .Get<DocumentIntelligenceOptions>() ?? new DocumentIntelligenceOptions();

if (diOptions.IsConfigured)
    builder.Services.AddSingleton<IDocumentExtractor, AzureDocumentExtractor>();
else
    builder.Services.AddSingleton<IDocumentExtractor, MockDocumentExtractor>();

// --- Field-rules: the type-aware matrix the user composes checks from. Registering a rule
// here makes it discoverable automatically via /api/field-rules and the UI. ---
builder.Services.AddSingleton<IFieldRule, RequiredCheck>();
builder.Services.AddSingleton<IFieldRule, PatternCheck>();
builder.Services.AddSingleton<IFieldRule, MinLengthCheck>();
builder.Services.AddSingleton<IFieldRule, ChecksumCheck>();
builder.Services.AddSingleton<IFieldRule, DateOnOrAfterCheck>();
builder.Services.AddSingleton<IFieldRule, DateOnOrBeforeCheck>();
builder.Services.AddSingleton<FieldCheckRunner>();

builder.Services.AddScoped<IVerificationService, VerificationService>();

var app = builder.Build();

app.UseExceptionHandler();

// Served in all environments: this is an open demo whose API is meant to be explored.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

// Single-app hosting (Azure App Service): when the built Vue frontend has been published into
// wwwroot, serve it from this process — same origin, so the SPA's relative /api calls just work.
// Locally there is no wwwroot (the Vite dev server + proxy is used instead), so this is a no-op
// and dev/tests behave exactly as before.
if (app.Environment.WebRootPath is { } webRoot && File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    // Client-side routing fallback; API and health routes above take precedence.
    app.MapFallbackToFile("index.html");
}

app.Logger.LogInformation("Document extractor active: {Mode}", diOptions.IsConfigured ? "azure" : "mock");

app.Run();

// Exposed so the integration tests can drive the real pipeline via WebApplicationFactory<Program>.
public partial class Program
{
}
