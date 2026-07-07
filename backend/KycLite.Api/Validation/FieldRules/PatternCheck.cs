using System.Text.RegularExpressions;
using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>The field value must match a user-supplied regular expression.</summary>
public sealed class PatternCheck : IFieldRule
{
    // Guards against catastrophic backtracking (ReDoS) on attacker-supplied patterns.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly string[] Types = [FieldTypes.Text];

    public string Key => "pattern";
    public string DisplayName => "Matches pattern";
    public string Description => "The field value must match the given regular expression.";
    public IReadOnlyList<string> AppliesTo => Types;
    public bool RequiresParam => true;
    public string? ParamLabel => "Pattern (regex), e.g. ^[A-Z0-9]+$";

    public FieldRuleOutcome Validate(string? value, string? param, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(param))
            return new FieldRuleOutcome(false, "No pattern provided.");
        if (string.IsNullOrEmpty(value))
            return new FieldRuleOutcome(false, "Value is missing.");

        try
        {
            return Regex.IsMatch(value, param, RegexOptions.None, MatchTimeout)
                ? new FieldRuleOutcome(true, $"Matches /{param}/.")
                : new FieldRuleOutcome(false, $"Does not match /{param}/.");
        }
        catch (ArgumentException)
        {
            return new FieldRuleOutcome(false, $"Invalid pattern /{param}/.");
        }
        catch (RegexMatchTimeoutException)
        {
            return new FieldRuleOutcome(false, "Pattern took too long to evaluate.");
        }
    }
}
