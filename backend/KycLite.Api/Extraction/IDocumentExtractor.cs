using KycLite.Api.Models;

namespace KycLite.Api.Extraction;

/// <summary>
/// The single boundary that hides the document-extraction provider from the rest of the app.
/// Swapping Azure for a mock (or any future provider) requires no change to callers, the API
/// contract, or the frontend.
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>"azure" or "mock" — used purely for transparency/logging.</summary>
    string Mode { get; }

    /// <summary>
    /// Extract every field the provider can read. The caller has already confirmed the stream is a
    /// supported format from its own leading bytes, so no content type is passed: the client's
    /// declared type is not evidence, and providers identify the format from the bytes anyway.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(Stream image, CancellationToken ct);
}
