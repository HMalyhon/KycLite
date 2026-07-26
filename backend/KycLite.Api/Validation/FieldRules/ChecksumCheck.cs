using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>
/// The machine-readable zone's embedded check digits must all be valid (ICAO 9303, 7-3-1). This is
/// an MRZ-only concept, so — unlike the other text rules — it applies solely to the MRZ field: a
/// printed name or document number carries no check digit, and offering it there is meaningless.
/// </summary>
public sealed class ChecksumCheck : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Mrz];

    public string Key => "checksum";
    public string DisplayName => "MRZ check digits (ICAO 9303)";
    public string Description => "The machine-readable zone's embedded check digits are all valid (ICAO 9303).";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => false;
    public string? ParamLabel => null;

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        var result = Mrz.Validate(value);
        return new FieldRuleOutcome(result.Valid, result.Message);
    }
}
