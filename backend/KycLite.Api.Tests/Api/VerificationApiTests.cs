using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KycLite.Api.Extraction;
using KycLite.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KycLite.Api.Tests.Api;

/// <summary>
/// Boots the real app (controllers + DI + mock extractor) over an in-memory test server.
/// </summary>
public class VerificationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    // The default check set the UI seeds with; every check passes against the mock document.
    private const string DefaultChecksJson = """
        [{"field":"dateOfBirth","rule":"dateOnOrBefore","param":"today-18y"},
         {"field":"dateOfExpiration","rule":"dateOnOrAfter","param":"today"},
         {"field":"documentNumber","rule":"checksum","param":null},
         {"field":"firstName","rule":"required","param":null},
         {"field":"lastName","rule":"required","param":null}]
        """;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public VerificationApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_WhenCalled_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert — assert on the status, not the framework's default body text.
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetStatus_WhenCalled_ReportsTheRegisteredExtractorsMode()
    {
        // Arrange — pin the extractor rather than relying on ambient DocumentIntelligence__*
        // environment variables, which would otherwise decide what this test sees.
        var extractor = new FakeDocumentExtractor(new ExtractionResult(), mode: "azure");
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IDocumentExtractor>();
                s.AddSingleton<IDocumentExtractor>(extractor);
            }))
            .CreateClient();

        // Act
        var status = await client.GetFromJsonAsync<ApiStatus>("/api/status", Json);

        // Assert — the page reads this on load; it must be the same mode a verify response reports.
        Assert.NotNull(status);
        Assert.Equal("azure", status.ExtractorMode);
    }

    [Fact]
    public async Task PostVerify_WhenExtractorThrows_ReturnsProblemDetails500()
    {
        // Arrange — swap in an extractor that throws; GlobalExceptionHandler should turn that into
        // an RFC 7807 ProblemDetails 500 rather than leaking a stack trace.
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IDocumentExtractor>();
                s.AddSingleton<IDocumentExtractor, ThrowingExtractor>();
            }))
            .CreateClient();
        using var content = BuildForm(fields: "*");

        // Act
        var response = await client.PostAsync("/api/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetFields_WhenCalled_ReturnsCatalog()
    {
        // Act
        var fields = await _client.GetFromJsonAsync<List<JsonElement>>("/api/fields", Json);

        // Assert
        Assert.NotNull(fields);
        Assert.Equal(9, fields!.Count);
    }

    [Fact]
    public async Task GetFieldRules_WhenCalled_ReturnsCatalog()
    {
        // Act
        var rules = await _client.GetFromJsonAsync<List<JsonElement>>("/api/field-rules", Json);

        // Assert — required, pattern, minLength, checksum, dateOnOrAfter, dateOnOrBefore.
        Assert.NotNull(rules);
        Assert.Equal(6, rules!.Count);
    }

    [Fact]
    public async Task GetDefaultChecks_WhenCalled_ReturnsSeedSet()
    {
        // Act
        var checks = await _client.GetFromJsonAsync<List<JsonElement>>("/api/default-checks", Json);

        // Assert
        Assert.NotNull(checks);
        Assert.Equal(5, checks!.Count);
    }

    [Fact]
    public async Task PostVerify_WithDefaultChecks_ApprovesViaMock()
    {
        // Arrange
        using var content = BuildForm(fields: "*", fieldChecks: DefaultChecksJson);

        // Act
        var response = await _client.PostAsync("/api/verify", content);
        var dto = await response.Content.ReadFromJsonAsync<VerifyDto>(Json);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(dto);
        Assert.Equal("Approve", dto!.Status);
        Assert.Equal("mock", dto.ExtractorMode);
        Assert.Equal(9, dto.ExtractedFields.Count);
        Assert.Equal(5, dto.RuleResults.Count);
        Assert.All(dto.RuleResults, r => Assert.True(r.Passed));
    }

    [Fact]
    public async Task PostVerify_WithFieldSelection_ProjectsRequestedFieldsOnly()
    {
        // Arrange
        using var content = BuildForm(fields: "firstName,lastName");

        // Act
        var response = await _client.PostAsync("/api/verify", content);
        var dto = await response.Content.ReadFromJsonAsync<VerifyDto>(Json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(2, dto!.ExtractedFields.Count);
        Assert.True(dto.ExtractedFields.ContainsKey("firstName"));
        Assert.True(dto.ExtractedFields.ContainsKey("lastName"));
    }

    [Fact]
    public async Task PostVerify_WithFailingFieldCheck_Rejects()
    {
        // Arrange — the mock first name "Erika" can't match a digits-only pattern, so this rejects.
        using var content = BuildForm(
            fields: "*",
            fieldChecks: """[{"field":"firstName","rule":"pattern","param":"^[0-9]+$"}]""");

        // Act
        var response = await _client.PostAsync("/api/verify", content);
        var dto = await response.Content.ReadFromJsonAsync<VerifyDto>(Json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("Reject", dto!.Status);
        Assert.Contains(dto.RuleResults, r => r.RuleKey == "firstName:pattern" && !r.Passed);
    }

    [Fact]
    public async Task PostVerify_WithUnknownFieldCheck_SurfacesItAsIgnored()
    {
        // Arrange — a check on a field that doesn't exist must not silently vanish.
        using var content = BuildForm(
            fields: "*",
            fieldChecks: """[{"field":"not-a-real-field","rule":"required","param":null}]""");

        // Act
        var response = await _client.PostAsync("/api/verify", content);
        var dto = await response.Content.ReadFromJsonAsync<VerifyDto>(Json);

        // Assert — no evaluated rule, but the dropped check is reported.
        Assert.NotNull(dto);
        Assert.Empty(dto!.RuleResults);
        Assert.Equal("not-a-real-field", Assert.Single(dto.IgnoredChecks).Field);
    }

    [Fact]
    public async Task PostVerify_WithMalformedFieldChecks_Returns400()
    {
        // Arrange
        using var content = BuildForm(fields: "*", fieldChecks: "{ not valid json");

        // Act
        var response = await _client.PostAsync("/api/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVerify_WithoutFile_Returns400()
    {
        // Arrange
        using var content = new MultipartFormDataContent
        {
            { new StringContent("*"), "fields" },
        };

        // Act
        var response = await _client.PostAsync("/api/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVerify_WithUnsupportedContentType_Returns400()
    {
        // Arrange
        using var content = BuildForm(fields: "*", contentType: "text/plain", fileName: "note.txt");

        // Act
        var response = await _client.PostAsync("/api/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVerify_WhenContentDoesNotMatchDeclaredType_Returns400()
    {
        // Arrange — a spoofed upload: declared image/png, but the bytes are not any supported format.
        var fileContent = new ByteArrayContent("not really an image"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "id.png" },
            { new StringContent("*"), "fields" },
        };

        // Act
        var response = await _client.PostAsync("/api/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent BuildForm(
        string fields,
        string? fieldChecks = null,
        string contentType = "image/png",
        string fileName = "id.png")
    {
        // A valid 8-byte PNG signature: the mock ignores the content, but the controller now
        // verifies the magic bytes match the upload's declared type before extracting.
        var fileContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var content = new MultipartFormDataContent
        {
            { fileContent, "file", fileName },
            { new StringContent(fields), "fields" },
        };
        if (fieldChecks is not null)
            content.Add(new StringContent(fieldChecks), "fieldChecks");
        return content;
    }

    private sealed record VerifyDto(
        string Status,
        string ExtractorMode,
        Dictionary<string, JsonElement> ExtractedFields,
        List<RuleDto> RuleResults,
        List<IgnoredDto> IgnoredChecks);

    private sealed record RuleDto(string RuleKey, bool Passed);

    private sealed record IgnoredDto(string Field, string Rule, string Reason);

    /// <summary>Extractor that always throws, to exercise the global exception handler.</summary>
    private sealed class ThrowingExtractor : IDocumentExtractor
    {
        public string Mode => "throwing";

        public Task<ExtractionResult> ExtractAsync(Stream image, string contentType, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }
}
