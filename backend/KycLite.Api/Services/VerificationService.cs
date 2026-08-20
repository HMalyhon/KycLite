using KycLite.Api.Catalog;
using KycLite.Api.Extraction;
using KycLite.Api.Models;
using KycLite.Api.Validation;

namespace KycLite.Api.Services;

/// <inheritdoc />
public sealed class VerificationService(
    IDocumentExtractor extractor,
    FieldCheckRunner checkRunner,
    TimeProvider clock) : IVerificationService
{
    private const string Wildcard = "*";

    public async Task<VerifyResponse> VerifyAsync(
        Stream image,
        string contentType,
        IEnumerable<string> selectedFields,
        IEnumerable<FieldCheck> fieldChecks,
        CancellationToken ct)
    {
        // Extraction always pulls every field; the response is projected afterwards, while
        // checks always evaluate against the full extraction.
        var extraction = await extractor.ExtractAsync(image, contentType, ct);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var run = checkRunner.Run(fieldChecks, extraction, today);
        var ruleResults = run.Evaluated.ToList();
        var approved = ruleResults.All(r => r.Passed); // nothing selected => vacuously approved

        var projection = ProjectFields(extraction.Fields, selectedFields);

        return new VerifyResponse
        {
            Status = approved ? "Approve" : "Reject",
            DocumentType = extraction.DocumentType,
            ExtractedFields = projection.Fields,
            RuleResults = ruleResults,
            IgnoredChecks = run.Ignored.ToList(),
            IgnoredFields = projection.Ignored,
            ExtractorMode = extractor.Mode,
        };
    }

    /// <summary>
    /// Narrows the extraction to the caller's selection, and reports back any requested key the
    /// field catalog doesn't know. Filtering alone would make a typo indistinguishable from a field
    /// the document simply didn't carry — the same silent drop <c>ignoredChecks</c> exists to
    /// prevent, applied to the other half of the request.
    /// </summary>
    private static (Dictionary<string, FieldValue> Fields, List<string> Ignored) ProjectFields(
        Dictionary<string, FieldValue> all,
        IEnumerable<string> selected)
    {
        var requested = selected.ToArray();
        var wanted = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Empty selection or "*" => return everything. Any other entries alongside the wildcard are
        // redundant rather than ignored — the caller asked for all fields and got them — so there is
        // nothing to report here.
        if (wanted.Count == 0 || wanted.Contains(Wildcard))
            return (all, []);

        // Only keys the catalog doesn't define count as ignored. A *known* field the extractor
        // didn't populate is absent, not dropped, and saying otherwise would cry wolf on every
        // document that legitimately lacks an address or a nationality.
        var ignored = requested
            .Where(key => !FieldCatalog.IsKnown(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fields = all
            .Where(kv => wanted.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return (fields, ignored);
    }
}
