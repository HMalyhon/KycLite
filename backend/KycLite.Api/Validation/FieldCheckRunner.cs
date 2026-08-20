using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Validation;

/// <summary>
/// Runs the user's field checks: for each (field, rule, param), resolves the field-rule and
/// validates it against that field's extracted value. This is the single verification mechanism —
/// the verdict is "approve" iff every check passes.
/// </summary>
public sealed class FieldCheckRunner(IEnumerable<IFieldRule> rules)
{
    private readonly Dictionary<string, IFieldRule> _rulesByKey =
        rules.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IFieldRule> AvailableRules => _rulesByKey.Values;

    public CheckRunOutcome Run(IEnumerable<FieldCheck> checks, ExtractionResult document, DateOnly today)
    {
        var evaluated = new List<RuleResult>();
        var ignored = new List<IgnoredCheck>();

        // Position in the submitted array, incremented before every guard so it stays correct across
        // the `continue`s below. This is what lets a caller tie each result — evaluated or ignored —
        // back to the check it sent, which "{field}:{rule}" cannot: two checks may share one.
        var index = -1;

        foreach (var check in checks)
        {
            index++;

            // A check that can't produce a meaningful verdict — a null/incomplete entry, an unknown
            // field or rule, a (field, rule) pair the type matrix doesn't allow, or (further down)
            // a param the rule can't interpret — is excluded from the verdict, so a hand-crafted
            // request can't manufacture a spurious rejection, but recorded as ignored, so the caller
            // isn't misled into thinking it passed. The JSON body can yield a null element
            // (fieldChecks=[null]) or null members regardless of the non-nullable record shape, so
            // the guards below are deliberately defensive.
            if (check is null)
            {
                ignored.Add(new IgnoredCheck(index, string.Empty, string.Empty, "Empty check."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(check.Field) || !FieldCatalog.IsKnown(check.Field))
            {
                ignored.Add(new IgnoredCheck(index, check.Field ?? string.Empty, check.Rule ?? string.Empty, "Unknown or missing field."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(check.Rule) || !_rulesByKey.TryGetValue(check.Rule, out var rule))
            {
                ignored.Add(new IgnoredCheck(index, check.Field, check.Rule ?? string.Empty, "Unknown or missing rule."));
                continue;
            }

            var fieldType = FieldCatalog.TypeOf(check.Field);
            if (fieldType is null || !rule.AppliesTo.Contains(fieldType))
            {
                ignored.Add(new IgnoredCheck(
                    index,
                    check.Field,
                    check.Rule,
                    $"Rule '{rule.Key}' does not apply to {fieldType ?? "this"}-type fields."));
                continue;
            }

            var value = document.Fields.TryGetValue(check.Field, out var field) ? field.Value : null;
            var outcome = rule.Validate(value, check.Param, today);

            // The rule resolved the field and the pair, but not the param — an unparseable minimum
            // length, an invalid regex, a date reference that means nothing. That's the same class
            // of malformed input as an unknown field or rule, and gets the same treatment: reported,
            // not counted. Only the *value* being unreadable is a genuine failure.
            if (!outcome.Evaluated)
            {
                ignored.Add(new IgnoredCheck(index, check.Field, check.Rule, outcome.Message));
                continue;
            }

            var label = string.IsNullOrWhiteSpace(check.Name)
                ? $"{FieldCatalog.Label(check.Field)} · {rule.DisplayName}"
                : check.Name;

            evaluated.Add(new RuleResult(index, $"{check.Field}:{rule.Key}", label, outcome.Passed, outcome.Message));
        }

        return new CheckRunOutcome(evaluated, ignored);
    }
}

/// <summary>The outcome of running a set of field checks: the evaluated results plus any that were ignored.</summary>
public sealed record CheckRunOutcome(IReadOnlyList<RuleResult> Evaluated, IReadOnlyList<IgnoredCheck> Ignored);
