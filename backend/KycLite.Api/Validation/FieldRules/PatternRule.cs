using System.Text.RegularExpressions;
using KycLite.Api.Catalog;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>The field value must match a user-supplied regular expression.</summary>
public sealed class PatternRule : IFieldRule
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
        // No pattern, or one the regex engine won't compile: the check is malformed, so it can't
        // be answered either way. A missing *value*, below, is a real failure — that's the document.
        if (string.IsNullOrWhiteSpace(param))
            return FieldRuleOutcome.CannotEvaluate("No pattern provided.");
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
            return FieldRuleOutcome.CannotEvaluate($"Invalid pattern /{param}/.");
        }
        catch (RegexMatchTimeoutException)
        {
            // A pattern that blows the ReDoS budget is one we never got an answer out of. Reporting
            // it as ignored costs nothing: the caller composes its own checks, so it could equally
            // have sent none — whereas failing here would reject a document on the rule's own
            // inability to run.
            return FieldRuleOutcome.CannotEvaluate($"Pattern /{param}/ took too long to evaluate.");
        }
    }
}
