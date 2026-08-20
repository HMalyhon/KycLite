using System.Text.Json;
using KycLite.Api.Models;
using KycLite.Api.Services;
using KycLite.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KycLite.Api.Controllers;

/// <summary>Accepts a document upload and returns the verification verdict.</summary>
[ApiController]
[Route("api")]
public sealed class VerificationController(IVerificationService verification) : ControllerBase
{
    /// <summary>Rate-limit policy name for the verify endpoint (configured in Program.cs).</summary>
    public const string RateLimitPolicy = "verify-upload";

    private const long MaxUploadBytes = 10 * 1024 * 1024;

    // Cap the whole multipart body a little above the file limit (form fields + boundaries add a
    // little), so an oversized *file* is caught by the explicit check below — with a clear message —
    // rather than tripping the framework's generic "request body too large" error first.
    private const long MaxRequestBytes = MaxUploadBytes + (1 * 1024 * 1024);

    // Derived from the signature table so the declared-type allowlist and the byte-level detector
    // can never drift apart: adding a format is still a single line in FileSignatures.
    private static readonly IReadOnlyList<string> AllowedContentTypes = FileSignatures.SupportedMediaTypes;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(VerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Verify([FromForm] VerifyForm form, CancellationToken ct)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
            return Problem(detail: "Missing 'file'.", statusCode: StatusCodes.Status400BadRequest);

        if (file.Length > MaxUploadBytes)
            return Problem(detail: "File exceeds the 10 MB limit.", statusCode: StatusCodes.Status400BadRequest);

        // Cheap first pass on the declared type, so an obviously unsupported upload is refused
        // without touching the stream. The bytes are still the authority — see below.
        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Problem(detail: $"Unsupported content type '{file.ContentType}'.", statusCode: StatusCodes.Status400BadRequest);

        FieldCheck[] fieldChecks;
        try
        {
            fieldChecks = ParseFieldChecks(form.FieldChecks);
        }
        catch (JsonException)
        {
            return Problem(detail: "Malformed 'fieldChecks' JSON.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();

        // The Content-Type header is client-supplied and trivially spoofable, so identify the file
        // from its own leading bytes.
        var detected = await DetectFormatAsync(stream, ct);
        if (detected is null)
            return Problem(detail: "File contents do not match a supported image or PDF format.", statusCode: StatusCodes.Status400BadRequest);

        // Both gates passing separately is not enough: PDF bytes sent as image/jpeg clear the
        // allowlist *and* the signature table while still being a lie about what was uploaded.
        // Require the two to agree, so the declared type is meaningful rather than merely present.
        if (!string.Equals(detected.MediaType, file.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                detail: $"Declared content type '{file.ContentType}' does not match the uploaded file, which is {detected.Name}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var response = await verification.VerifyAsync(
            stream,
            ParseCsv(form.Fields),
            fieldChecks,
            ct);

        return Ok(response);
    }

    /// <summary>
    /// Reads the leading bytes of the upload, identifies the format from its signature (see
    /// <see cref="FileSignatures"/>), then rewinds the stream for the extractor. Returns null when
    /// the bytes match no supported format. Requires a seekable stream, which IFormFile provides.
    /// </summary>
    private static async Task<DetectedFormat?> DetectFormatAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[FileSignatures.MaxSignatureLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        stream.Seek(0, SeekOrigin.Begin);

        return FileSignatures.Detect(header.AsSpan(0, read));
    }

    private static string[] ParseCsv(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static FieldCheck[] ParseFieldChecks(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<FieldCheck[]>(json, JsonOptions) ?? [];
}
