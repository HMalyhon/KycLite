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
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var dateOfBirth = new DateOnly(1990, 1, 15);
        var dateOfExpiration = today.AddYears(5);

        var result = new ExtractionResult
        {
            DocumentType = "passport",
            // Match ExtractionResult's case-insensitive default so the mock and Azure paths
            // resolve mixed-case field keys identically.
            Fields = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase)
            {
                [FieldKeys.FirstName] = new("Erika", 0.991),
                [FieldKeys.LastName] = new("Mustermann", 0.987),
                // The document number as printed (VIZ) — no check digit; that lives in the MRZ.
                [FieldKeys.DocumentNumber] = new("L898902C", 0.972),
                [FieldKeys.DateOfBirth] = new(dateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0.965),
                [FieldKeys.DateOfExpiration] = new(dateOfExpiration.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0.958),
                [FieldKeys.Sex] = new("F", 0.94),
                [FieldKeys.Nationality] = new("UTO", 0.93),
                [FieldKeys.CountryRegion] = new("UTO", 0.93),
                [FieldKeys.Address] = new("123 Example Street, Sampletown", 0.81),
                // A real TD3 MRZ with every check digit computed, so the checksum rule passes
                // against the mock exactly as it would for a genuine passport.
                [FieldKeys.MachineReadableZone] = new(BuildTd3Mrz(dateOfBirth, dateOfExpiration), 0.95),
            },
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Assembles a valid ICAO 9303 TD3 (passport) MRZ for the sample identity, computing each
    /// embedded check digit so <see cref="Mrz"/> validates it. The expiry date is dynamic, so the
    /// second line must be built at runtime rather than hard-coded.
    /// </summary>
    private static string BuildTd3Mrz(DateOnly dateOfBirth, DateOnly dateOfExpiration)
    {
        // Line 1: document type, issuing country, surname << given names, filler-padded to 44.
        var line1 = "P<UTOMUSTERMANN<<ERIKA".PadRight(44, '<');

        // Line 2 body: number(9)+check, nationality, DOB+check, sex, expiry+check, personal(14)+check.
        const string documentNumber = "L898902C<"; // 9-char field: "L898902C" padded with a filler
        var dob = dateOfBirth.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var expiry = dateOfExpiration.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var personalNumber = new string('<', 14);

        var body =
            documentNumber + CheckDigit(documentNumber) +
            "UTO" +
            dob + CheckDigit(dob) +
            "F" +
            expiry + CheckDigit(expiry) +
            personalNumber + CheckDigit(personalNumber);

        // Composite check over number+check, DOB+check, and expiry+check .. personal+check.
        var composite = body[0..10] + body[13..20] + body[21..43];
        var line2 = body + CheckDigit(composite);

        return line1 + "\n" + line2;
    }

    private static char CheckDigit(string data) => (char)('0' + Mrz731.ComputeCheckDigit(data));
}
