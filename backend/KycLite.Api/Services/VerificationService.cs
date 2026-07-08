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

        return new VerifyResponse
        {
            Status = approved ? "Approve" : "Reject",
            DocumentType = extraction.DocumentType,
            ExtractedFields = ProjectFields(extraction.Fields, selectedFields),
            RuleResults = ruleResults,
            IgnoredChecks = run.Ignored.ToList(),
            ExtractorMode = extractor.Mode,
        };
    }

    private static Dictionary<string, FieldValue> ProjectFields(
        Dictionary<string, FieldValue> all,
        IEnumerable<string> selected)
    {
        var wanted = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Empty selection or "*" => return everything.
        if (wanted.Count == 0 || wanted.Contains(Wildcard))
            return all;

        return all
            .Where(kv => wanted.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
