namespace KycLite.Api.Models;

/// <summary>
/// A single extracted field. Provider-agnostic: the Azure or mock extractor both
/// populate this shape, so nothing downstream knows where the data came from.
/// </summary>
public sealed record FieldValue(string Value, double? Confidence);

/// <summary>
/// The full, provider-agnostic result of extracting a document. Fields are keyed by the
/// canonical keys defined in <see cref="Catalog.FieldCatalog"/>.
/// </summary>
public sealed class ExtractionResult
{
    public string? DocumentType { get; set; }

    // Case-insensitive so a check keyed "DateOfBirth" resolves the same value as "dateOfBirth"
    // (rule and field-selection lookups are already ordinal-ignore-case).
    public Dictionary<string, FieldValue> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
