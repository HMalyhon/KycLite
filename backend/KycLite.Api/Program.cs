using System.Globalization;
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
// If a variable already set in the real environment (or via .NET user-secrets) wins, which
// is the precedence the README documents and the safer default on a host
Env.TraversePath().NoClobber().Load();

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

    // Answer a throttled caller with the same RFC 7807 shape every other API error uses (the
    // default rejection writes an empty body), and tell them when the window reopens.
    options.OnRejected = async (context, _) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Rate limit exceeded. Please retry shortly.",
            },
        });
    };

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

// --- Extraction provider: Azure when an endpoint is configured, otherwise the offline mock.
// The key is optional — without one the extractor authenticates with Entra ID (the App Service's
// managed identity in Azure, the developer's `az login` locally), so no secret is deployed. ---
builder.Services
    .AddOptions<DocumentIntelligenceOptions>()
    .Bind(builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName))
    // When an endpoint is set, fail fast at startup on a malformed one rather than on the first
    // upload. https is required, not merely an absolute URI: the extractor now sends an Entra
    // access token to this host, and a token must never leave over plaintext.
    // In mock mode (nothing set) the predicate is a no-op.
    .Validate(
        o => !o.IsConfigured
            || (Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps),
        "DocumentIntelligence__Endpoint must be an absolute https URI, e.g. https://<resource>.cognitiveservices.azure.com/.")
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
builder.Services.AddSingleton<IFieldRule, RequiredRule>();
builder.Services.AddSingleton<IFieldRule, PatternRule>();
builder.Services.AddSingleton<IFieldRule, MinLengthRule>();
builder.Services.AddSingleton<IFieldRule, ChecksumRule>();
builder.Services.AddSingleton<IFieldRule, DateOnOrAfterRule>();
builder.Services.AddSingleton<IFieldRule, DateOnOrBeforeRule>();
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

// Unmatched /api/* requests must not fall through to the SPA shell (or a blank 404): answer them
// as RFC 7807, like every other API error. This pattern carries a literal "api" segment, so it
// out-ranks the catch-all SPA fallback below for API paths; registered unconditionally so the
// behaviour is identical with or without a bundled frontend.
app.MapFallback("/api/{**rest}", async (HttpContext http, IProblemDetailsService problemDetails) =>
{
    http.Response.StatusCode = StatusCodes.Status404NotFound;
    await problemDetails.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = http,
        ProblemDetails =
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found.",
            Detail = $"No API endpoint matches '{http.Request.Path}'.",
        },
    });
});

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

app.Logger.LogInformation(
    "Document extractor active: {Mode} ({Auth})",
    diOptions.IsConfigured ? "azure" : "mock",
    diOptions.IsConfigured ? (diOptions.UsesManagedIdentity ? "Entra ID / managed identity" : "account key") : "offline");

app.Run();

// Exposed so the integration tests can drive the real pipeline via WebApplicationFactory<Program>.
public partial class Program
{
}
