using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>
/// The date field must be on or before (≤, "lower") the given date. Replicates "age ≥ 18"
/// via the parameter "today-18y" (date of birth on or before today minus 18 years).
/// </summary>
public sealed class DateOnOrBeforeCheck : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Date];

    public string Key => "dateOnOrBefore";
    public string DisplayName => "On or before (≤)";
    public string Description => "The date must be less than or equal to the given date.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => true;
    public string? ParamLabel => "Date or today±offset, e.g. today-18y";

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        if (!DateParsing.TryParseValue(value, out var date))
            return new FieldRuleOutcome(false, "Value missing or unreadable.");
        if (!DateParsing.TryResolveReference(param, today, out var reference))
            return new FieldRuleOutcome(false, $"Invalid date '{param}'.");

        return date <= reference
            ? new FieldRuleOutcome(true, $"{date:yyyy-MM-dd} is on or before {reference:yyyy-MM-dd}.")
            : new FieldRuleOutcome(false, $"{date:yyyy-MM-dd} is after {reference:yyyy-MM-dd}.");
    }
}
