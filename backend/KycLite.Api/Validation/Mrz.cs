namespace KycLite.Api.Validation;

/// <summary>The outcome of validating a machine-readable zone's check digits.</summary>
public sealed record MrzResult(bool Valid, string Message);

/// <summary>
/// Validates the check digits embedded in an ICAO 9303 machine-readable zone (MRZ). Supports the
/// two common layouts — TD3 (passport, 2×44) and TD1 (ID card, 3×30) — verifying each field's
/// own check digit plus the composite digit with the shared 7-3-1 algorithm (<see cref="Mrz731"/>).
/// TD2 (2×36) is a straightforward addition to the length switch if ever needed.
/// </summary>
public static class Mrz
{
    public static MrzResult Validate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new MrzResult(false, "MRZ is missing.");

        // Azure and OCR generally hand back the MRZ as two/three lines separated by newlines; strip
        // every whitespace character and upper-case so the fixed-width layout below lines up.
        var mrz = Compact(raw);

        if (!mrz.All(IsMrzChar))
            return new MrzResult(false, "MRZ contains characters outside the A–Z 0–9 < alphabet.");

        return mrz.Length switch
        {
            88 => ValidateTd3(mrz),
            90 => ValidateTd1(mrz),
            _ => new MrzResult(false, $"Unrecognized MRZ length ({mrz.Length}); expected 88 (TD3) or 90 (TD1)."),
        };
    }

    /// <summary>
    /// Recovers a machine-readable zone from raw OCR text. The prebuilt-idDocument model reads the
    /// front (visual zone) and returns no structured MRZ for a back/MRZ-only image, yet the OCR text
    /// still contains the MRZ lines (often with stray spaces between groups). Finds the run of lines
    /// that, once whitespace is stripped, form a TD3 (2×44) or TD1 (3×30) block over the MRZ
    /// alphabet. Returns the reassembled MRZ, or null when none is present.
    /// </summary>
    public static string? ExtractFromText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var lines = content
            .Split('\n', '\r')
            .Select(Compact)
            .Where(line => line.Length >= 28 && line.All(IsMrzChar))
            .ToArray();

        // TD3: two 44-char lines. Require a filler so an incidental block of text can't match.
        for (var i = 0; i + 2 <= lines.Length; i++)
        {
            if (lines[i].Length == 44 && lines[i + 1].Length == 44)
            {
                var mrz = lines[i] + lines[i + 1];
                if (mrz.Contains('<')) return mrz;
            }
        }

        // TD1: three 30-char lines.
        for (var i = 0; i + 3 <= lines.Length; i++)
        {
            if (lines[i].Length == 30 && lines[i + 1].Length == 30 && lines[i + 2].Length == 30)
            {
                var mrz = lines[i] + lines[i + 1] + lines[i + 2];
                if (mrz.Contains('<')) return mrz;
            }
        }

        return null;
    }

    // TD3 (passport): two 44-character lines. All check digits live on line 2.
    private static MrzResult ValidateTd3(string mrz)
    {
        var line2 = mrz[44..];

        if (!DigitMatches(line2[0..9], line2[9])) return Invalid("document number");
        if (!DigitMatches(line2[13..19], line2[19])) return Invalid("date of birth");
        if (!DigitMatches(line2[21..27], line2[27])) return Invalid("expiry date");
        if (!DigitMatches(line2[28..42], line2[42])) return Invalid("personal number");

        var composite = line2[0..10] + line2[13..20] + line2[21..43];
        if (!DigitMatches(composite, line2[43])) return Invalid("composite");

        return new MrzResult(true, "All TD3 MRZ check digits are valid.");
    }

    // TD1 (ID card): three 30-character lines. The document-number check is on line 1; the date and
    // composite checks are on line 2. Line 3 is names and carries no check digit.
    private static MrzResult ValidateTd1(string mrz)
    {
        var line1 = mrz[0..30];
        var line2 = mrz[30..60];

        if (!DigitMatches(line1[5..14], line1[14])) return Invalid("document number");
        if (!DigitMatches(line2[0..6], line2[6])) return Invalid("date of birth");
        if (!DigitMatches(line2[8..14], line2[14])) return Invalid("expiry date");

        var composite = line1[5..30] + line2[0..7] + line2[8..15] + line2[18..29];
        if (!DigitMatches(composite, line2[29])) return Invalid("composite");

        return new MrzResult(true, "All TD1 MRZ check digits are valid.");
    }

    // Upper-case and strip every whitespace character, leaving the fixed-width MRZ characters.
    private static string Compact(string raw) =>
        new(raw.Where(c => !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());

    private static bool DigitMatches(string data, char checkDigit) =>
        char.IsDigit(checkDigit) && Mrz731.ComputeCheckDigit(data) == checkDigit - '0';

    private static bool IsMrzChar(char c) => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '<';

    private static MrzResult Invalid(string field) =>
        new(false, $"The MRZ {field} check digit is invalid.");
}
