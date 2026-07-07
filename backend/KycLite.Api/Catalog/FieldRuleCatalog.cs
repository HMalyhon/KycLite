using KycLite.Api.Models;
using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Catalog;

/// <summary>
/// Projects the registered <see cref="IFieldRule"/> set into the descriptors served by
/// <c>GET /api/field-rules</c>. Each descriptor lists the field types it applies to so the
/// frontend can build the field-type → rule matrix.
/// </summary>
public static class FieldRuleCatalog
{
    public static IReadOnlyList<FieldRuleDescriptor> Describe(IEnumerable<IFieldRule> rules)
        => rules
            .Select(r => new FieldRuleDescriptor(
                r.Key, r.DisplayName, r.Description, r.RequiresParam, r.ParamLabel, r.AppliesTo))
            .ToList();
}
