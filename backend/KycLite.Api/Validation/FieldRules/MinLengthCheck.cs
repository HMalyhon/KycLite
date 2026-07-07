using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>The field value must be at least N characters (after trimming).</summary>
public sealed class MinLengthCheck : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Text];

    public string Key => "minLength";
    public string DisplayName => "Minimum length";
    public string Description => "The field value must be at least the given number of characters.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => true;
    public string? ParamLabel => "Minimum length";

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        if (!int.TryParse(param, out var min) || min < 0)
            return new FieldRuleOutcome(false, "Invalid minimum length.");

        var length = value?.Trim().Length ?? 0;
        return length >= min
            ? new FieldRuleOutcome(true, $"Length {length} ≥ {min}.")
            : new FieldRuleOutcome(false, $"Length {length} < {min}.");
    }
}
