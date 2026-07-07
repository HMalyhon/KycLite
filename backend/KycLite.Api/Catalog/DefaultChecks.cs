using KycLite.Api.Models;

namespace KycLite.Api.Catalog;

/// <summary>
/// The default field-check set the UI is seeded with — reproduces the legacy fixed rules
/// (age ≥ 18, not expired, document-number checksum, first/last name present) using the
/// type-aware field-rule matrix. Served by <c>GET /api/default-checks</c>.
/// </summary>
public static class DefaultChecks
{
    public static readonly IReadOnlyList<FieldCheck> All =
    [
        new(FieldKeys.DateOfBirth, "dateOnOrBefore", "today-18y", "Age ≥ 18"),
        new(FieldKeys.DateOfExpiration, "dateOnOrAfter", "today", "Not expired"),
        new(FieldKeys.DocumentNumber, "checksum", null, "Document number checksum"),
        new(FieldKeys.FirstName, "required", null, "First name present"),
        new(FieldKeys.LastName, "required", null, "Last name present"),
    ];
}
