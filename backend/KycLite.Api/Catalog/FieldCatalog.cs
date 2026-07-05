using KycLite.Api.Models;

namespace KycLite.Api.Catalog;

/// <summary>Canonical field keys used across extractors, rules, and the API contract.</summary>
public static class FieldKeys
{
    public const string FirstName = "firstName";
    public const string LastName = "lastName";
    public const string DocumentNumber = "documentNumber";
    public const string DateOfBirth = "dateOfBirth";
    public const string DateOfExpiration = "dateOfExpiration";
    public const string Sex = "sex";
    public const string Nationality = "nationality";
    public const string CountryRegion = "countryRegion";
    public const string Address = "address";
}

/// <summary>Field data types — the rows of the field-rule matrix.</summary>
public static class FieldTypes
{
    public const string Text = "text";
    public const string Date = "date";
}

/// <summary>
/// Single source of truth for the fields the web app can request, each tagged with a type so the
/// frontend offers only the field-rules that apply. Drives the <c>GET /api/fields</c> endpoint.
/// </summary>
public static class FieldCatalog
{
    public static readonly IReadOnlyList<FieldDescriptor> All =
    [
        new(FieldKeys.FirstName, "First name", FieldTypes.Text),
        new(FieldKeys.LastName, "Last name", FieldTypes.Text),
        new(FieldKeys.DocumentNumber, "Document number", FieldTypes.Text),
        new(FieldKeys.DateOfBirth, "Date of birth", FieldTypes.Date),
        new(FieldKeys.DateOfExpiration, "Expiry date", FieldTypes.Date),
        new(FieldKeys.Sex, "Sex", FieldTypes.Text),
        new(FieldKeys.Nationality, "Nationality", FieldTypes.Text),
        new(FieldKeys.CountryRegion, "Country / region", FieldTypes.Text),
        new(FieldKeys.Address, "Address", FieldTypes.Text),
    ];

    private static readonly Dictionary<string, FieldDescriptor> ByKey =
        All.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);

    /// <summary>Human label for a field key, falling back to the key itself.</summary>
    public static string Label(string key) => ByKey.TryGetValue(key, out var f) ? f.Label : key;

    /// <summary>Field type for a key, or null if unknown.</summary>
    public static string? TypeOf(string key) => ByKey.TryGetValue(key, out var f) ? f.Type : null;
}
