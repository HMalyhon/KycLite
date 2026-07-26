using KycLite.Api.Models;

namespace KycLite.Api.Catalog;

/// <summary>
/// The default field-check set the UI is seeded with — age ≥ 18, not expired, a document-number
/// format check, and first/last name present — expressed with the type-aware field-rule matrix.
/// The MRZ checksum is deliberately not seeded: it runs only when a user adds it, so the app
/// doesn't assume every upload carries a (passport/back-of-card) MRZ. Served by
/// <c>GET /api/default-checks</c>.
/// </summary>
public static class DefaultChecks
{
    public static readonly IReadOnlyList<FieldCheck> All =
    [
        new(FieldKeys.DateOfBirth, "dateOnOrBefore", "today-18y", "Age ≥ 18"),
        new(FieldKeys.DateOfExpiration, "dateOnOrAfter", "today", "Not expired"),
        new(FieldKeys.DocumentNumber, "pattern", "^[A-Z0-9]+$", "Document number format"),
        new(FieldKeys.FirstName, "required", null, "First name present"),
        new(FieldKeys.LastName, "required", null, "Last name present"),
    ];
}
