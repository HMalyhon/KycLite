using KycLite.Api.Extraction;
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

// Interactive API docs (Swagger UI at /swagger). The discovery-driven endpoints are the
// centrepiece of this demo, so they're worth exploring in the browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KYC-Lite API",
        Version = "v1",
        Description = "ID/passport verification demo: extract document fields.",
    });

    // Fold in the XML summaries emitted by GenerateDocumentationFile, when present.
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "KycLite.Api.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// Liveness probe for containers/uptime monitoring.
builder.Services.AddHealthChecks();

// Offline, network-free extractor. A real provider will later sit behind the same
// IDocumentExtractor boundary with no change to callers or the API contract.
builder.Services.AddSingleton<IDocumentExtractor, MockDocumentExtractor>();

var app = builder.Build();

// Served in all environments: this is an open demo whose API is meant to be explored.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicy);
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
