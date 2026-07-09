using KycLite.Api.Catalog;
using KycLite.Api.Extraction;
using KycLite.Api.Models;

namespace KycLite.Api.Tests;

/// <summary>Fluent-ish builder for an <see cref="ExtractionResult"/> in tests.</summary>
internal static class Doc
{
    public static ExtractionResult With(params (string Key, string Value)[] fields)
    {
        var result = new ExtractionResult { DocumentType = "passport" };
        foreach (var (key, value) in fields)
            result.Fields[key] = new FieldValue(value, 0.99);
        return result;
    }

    /// <summary>A fully-valid sample document (all rules pass against a 2025-01-01 reference).</summary>
    public static ExtractionResult Valid() => With(
        (FieldKeys.FirstName, "Erika"),
        (FieldKeys.LastName, "Mustermann"),
        (FieldKeys.DocumentNumber, "L898902C3"), // valid ICAO trailing check digit
        (FieldKeys.DateOfBirth, "1990-01-15"),
        (FieldKeys.DateOfExpiration, "2031-06-29"));
}

/// <summary>Configurable extractor so service tests don't depend on Azure or the real mock.</summary>
internal sealed class FakeDocumentExtractor(ExtractionResult result, string mode = "fake") : IDocumentExtractor
{
    public string Mode { get; } = mode;

    public Task<ExtractionResult> ExtractAsync(Stream image, string contentType, CancellationToken ct)
        => Task.FromResult(result);
}

/// <summary>A TimeProvider frozen at a fixed instant, so date-driven rules are deterministic.</summary>
internal sealed class FixedTimeProvider(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
