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

    Task<ExtractionResult> ExtractAsync(Stream image, string contentType, CancellationToken ct);
}
