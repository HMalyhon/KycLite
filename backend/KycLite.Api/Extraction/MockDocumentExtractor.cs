using System.Globalization;
using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Validation;

namespace KycLite.Api.Extraction;

/// <summary>
/// Deterministic, network-free extractor used when no Azure credentials are configured.
/// Returns a valid sample identity so the full pipeline (and the demo) works offline.
/// </summary>
public sealed class MockDocumentExtractor(TimeProvider clock) : IDocumentExtractor
{
    public string Mode => "mock";

    public Task<ExtractionResult> ExtractAsync(Stream image, string contentType, CancellationToken ct)
    {
        // Build a document number whose trailing digit is a valid ICAO 7-3-1 check digit,
        // so the checksum rule passes against the mock just as it would for a real document.
        const string baseNumber = "L898902C";
        var documentNumber = baseNumber + Mrz731.ComputeCheckDigit(baseNumber);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var result = new ExtractionResult
        {
            DocumentType = "passport",
            // Match ExtractionResult's case-insensitive default so the mock and Azure paths
            // resolve mixed-case field keys identically.
            Fields = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase)
            {
                [FieldKeys.FirstName] = new("Erika", 0.991),
                [FieldKeys.LastName] = new("Mustermann", 0.987),
                [FieldKeys.DocumentNumber] = new(documentNumber, 0.972),
                [FieldKeys.DateOfBirth] = new("1990-01-15", 0.965),
                [FieldKeys.DateOfExpiration] = new(today.AddYears(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0.958),
                [FieldKeys.Sex] = new("F", 0.94),
                [FieldKeys.Nationality] = new("UTO", 0.93),
                [FieldKeys.CountryRegion] = new("UTO", 0.93),
                [FieldKeys.Address] = new("123 Example Street, Sampletown", 0.81),
            },
        };

        return Task.FromResult(result);
    }
}
