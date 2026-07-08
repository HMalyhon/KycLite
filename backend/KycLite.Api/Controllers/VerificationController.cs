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

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/tiff", "application/pdf"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(MaxUploadBytes)]
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

        // The Content-Type header is client-supplied and trivially spoofable; verify the actual
        // file signature (magic bytes) so a request can't smuggle arbitrary bytes past the type gate.
        if (!await HasSupportedSignatureAsync(stream, ct))
            return Problem(detail: "File contents do not match a supported image or PDF format.", statusCode: StatusCodes.Status400BadRequest);

        var response = await verification.VerifyAsync(
            stream,
            file.ContentType,
            ParseCsv(form.Fields),
            fieldChecks,
            ct);

        return Ok(response);
    }

    /// <summary>
    /// Reads the leading bytes of the upload, confirms they match a supported format's signature
    /// (see <see cref="FileSignatures"/>), then rewinds the stream for the extractor. Guards against
    /// a spoofed Content-Type header. Requires a seekable stream, which IFormFile provides.
    /// </summary>
    private static async Task<bool> HasSupportedSignatureAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[FileSignatures.MaxSignatureLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        stream.Seek(0, SeekOrigin.Begin);

        return FileSignatures.IsSupported(header.AsSpan(0, read));
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
