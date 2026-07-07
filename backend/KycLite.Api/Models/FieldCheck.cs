namespace KycLite.Api.Models;

/// <summary>
/// A user-defined check: apply field-rule <see cref="Rule"/> to <see cref="Field"/>.
/// <see cref="Name"/> is an optional custom label shown in the results.
/// </summary>
public sealed record FieldCheck(string Field, string Rule, string? Param, string? Name = null);

/// <summary>Describes a field-rule the user can attach to a field of a matching type.</summary>
public sealed record FieldRuleDescriptor(
    string Key,
    string Label,
    string Description,
    bool RequiresParam,
    string? ParamLabel,
    IReadOnlyList<string> AppliesTo);
