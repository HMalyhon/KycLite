namespace KycLite.Api.Models;

/// <summary>
/// Describes a field the user can choose to include in the response. <see cref="Type"/> is one of
/// the <see cref="Catalog.FieldTypes"/> values and drives which field-rules apply (the matrix).
/// </summary>
public sealed record FieldDescriptor(string Key, string Label, string Type);

/// <summary>
/// Runtime state the web app reads on load. <paramref name="ExtractorMode"/> is "azure" or "mock",
/// matching <see cref="VerifyResponse.ExtractorMode"/>, so the UI can say which engine is live
/// before the first upload.
/// </summary>
public sealed record ApiStatus(string ExtractorMode);

/// <summary>Outcome of a single check.</summary>
public sealed record RuleResult(string RuleKey, string RuleLabel, bool Passed, string Message);

/// <summary>
/// A check that could not be evaluated (unknown field/rule, or a rule that doesn't apply to the
/// field's type) and was therefore excluded from the verdict. Surfaced so the caller can tell a
/// check was ignored rather than silently passed.
/// </summary>
public sealed record IgnoredCheck(string Field, string Rule, string Reason);

/// <summary>The verdict returned to the web app.</summary>
public sealed class VerifyResponse
{
    /// <summary>"Approve" when every check passed; otherwise "Reject".</summary>
    public required string Status { get; init; }
    public string? DocumentType { get; init; }

    /// <summary>Extracted fields projected to the user's selection (all fields when "*").</summary>
    public Dictionary<string, FieldValue> ExtractedFields { get; init; } = new();
    public List<RuleResult> RuleResults { get; init; } = new();

    /// <summary>Checks that were dropped without evaluating (see <see cref="IgnoredCheck"/>); empty when all ran.</summary>
    public List<IgnoredCheck> IgnoredChecks { get; init; } = new();

    /// <summary>"azure" or "mock" — surfaced for transparency in the demo.</summary>
    public required string ExtractorMode { get; init; }
}
