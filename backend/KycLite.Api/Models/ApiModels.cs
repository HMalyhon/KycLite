namespace KycLite.Api.Models;

/// <summary>
/// Describes a field the user can choose to include in the response. <see cref="Type"/> is one of
/// the <see cref="Catalog.FieldTypes"/> values and drives which field-rules apply (the matrix).
/// </summary>
public sealed record FieldDescriptor(string Key, string Label, string Type);
