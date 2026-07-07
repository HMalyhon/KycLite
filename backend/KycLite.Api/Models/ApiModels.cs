namespace KycLite.Api.Models;

/// <summary>
/// Describes a field the user can choose to include in the response. <see cref="Type"/> is one of
/// the <see cref="Catalog.FieldTypes"/> values and drives which field-rules apply (the matrix).
/// </summary>
public sealed record FieldDescriptor(string Key, string Label, string Type);

/// <summary>Outcome of a single check.</summary>
public sealed record RuleResult(string RuleKey, string RuleLabel, bool Passed, string Message);

/// <summary>
/// A check that could not be evaluated (unknown field/rule, or a rule that doesn't apply to the
/// field's type) and was therefore excluded from the verdict. Surfaced so the caller can tell a
/// check was ignored rather than silently passed.
/// </summary>
public sealed record IgnoredCheck(string Field, string Rule, string Reason);
