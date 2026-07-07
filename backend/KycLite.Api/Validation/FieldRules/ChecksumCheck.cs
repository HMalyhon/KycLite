using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>
/// The field value must carry a valid ICAO 9303 trailing check digit (7-3-1 weighting).
/// Replaces the old document-number checksum rule, now applicable to any text field.
/// </summary>
public sealed class ChecksumCheck : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Text];

    public string Key => "checksum";
    public string DisplayName => "Checksum (ICAO 9303)";
    public string Description => "The value must end in a valid ICAO 9303 (7-3-1) check digit.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => false;
    public string? ParamLabel => null;

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new FieldRuleOutcome(false, "Value is missing.");

        return Mrz731.ValidateWithTrailingCheckDigit(value)
            ? new FieldRuleOutcome(true, $"Checksum valid for '{value}'.")
            : new FieldRuleOutcome(false, $"Checksum invalid for '{value}'.");
    }
}
