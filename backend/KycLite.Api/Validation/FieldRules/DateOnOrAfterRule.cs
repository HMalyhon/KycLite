using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>
/// The date field must be on or after (≥, "greater") the given date. Replicates "not expired"
/// via the parameter "today".
/// </summary>
public sealed class DateOnOrAfterRule : IFieldRule
{
    private static readonly string[] Types = [FieldTypes.Date];

    public string Key => "dateOnOrAfter";
    public string DisplayName => "On or after (≥)";
    public string Description => "The date must be greater than or equal to the given date.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => true;
    public string? ParamLabel => "Date or today±offset, e.g. today";

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        // Resolve the reference first: if the question is malformed there is nothing to ask of the
        // document, whatever its value says. An unreadable value, by contrast, is a real failure.
        if (!DateParsing.TryResolveReference(param, today, out var reference))
            return FieldRuleOutcome.CannotEvaluate($"Invalid date '{param}'.");
        if (!DateParsing.TryParseValue(value, out var date))
            return new FieldRuleOutcome(false, "Value missing or unreadable.");

        return date >= reference
            ? new FieldRuleOutcome(true, $"{date:yyyy-MM-dd} is on or after {reference:yyyy-MM-dd}.")
            : new FieldRuleOutcome(false, $"{date:yyyy-MM-dd} is before {reference:yyyy-MM-dd}.");
    }
}
