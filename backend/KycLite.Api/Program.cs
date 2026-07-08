using System.Threading.RateLimiting;
using KycLite.Api.Controllers;
using KycLite.Api.Extraction;
using KycLite.Api.Infrastructure;
using KycLite.Api.Services;
using KycLite.Api.Validation;
using KycLite.Api.Validation.FieldRules;
using Microsoft.OpenApi;

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

// Guard the verify endpoint (each real call can incur a billed provider transaction) with a
// per-client fixed window, so a single caller can't drive cost or starve others.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(VerificationController.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0,
            }));
});

// Consistent RFC 7807 error responses: unhandled exceptions become ProblemDetails
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

// Offline, network-free extractor. A real provider will later sit behind the same
// IDocumentExtractor boundary with no change to callers or the API contract.
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

app.Run();
