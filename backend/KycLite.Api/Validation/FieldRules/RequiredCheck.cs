using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>The field must be present and non-empty. Applies to any field type.</summary>
public sealed class RequiredCheck : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Text, FieldTypes.Date];

    public string Key => "required";
    public string DisplayName => "Required";
    public string Description => "The field must be present and non-empty.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => false;
    public string? ParamLabel => null;

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
        => string.IsNullOrWhiteSpace(value)
            ? new FieldRuleOutcome(false, "Value is missing.")
            : new FieldRuleOutcome(true, "Present.");
}
