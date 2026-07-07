namespace KycLite.Api.Validation.FieldRules;

/// <summary>Result of running a field rule against a single field value.</summary>
public sealed record FieldRuleOutcome(bool Passed, string Message);

/// <summary>
/// A generic check the user attaches to a field of a matching type (the field-rule matrix).
/// Replaces the old fixed document-level rules — the default check set reproduces them.
/// </summary>
public interface IFieldRule
{
    /// <summary>Stable key the frontend selects by (e.g. "required").</summary>
    string Key { get; }

    string DisplayName { get; }
    string Description { get; }

    /// <summary>Field types (see <see cref="Catalog.FieldTypes"/>) this rule can be applied to.</summary>
    IReadOnlyList<string> AppliesTo { get; }

    /// <summary>Whether the rule needs a user-supplied parameter (regex, length, date, …).</summary>
    bool RequiresParam { get; }

    /// <summary>Label/hint for the parameter input, when one is required.</summary>
    string? ParamLabel { get; }

    /// <summary>
    /// Validate the extracted field value (null when the field was not extracted).
    /// <paramref name="today"/> is the reference date for relative date comparisons.
    /// </summary>
    FieldRuleOutcome Validate(string? value, string? param, DateOnly today);
}
