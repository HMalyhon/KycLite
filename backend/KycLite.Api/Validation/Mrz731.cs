namespace KycLite.Api.Validation;

/// <summary>
/// ICAO 9303 "7-3-1" check-digit algorithm used in machine-readable travel documents.
/// Letters map A=10..Z=35, digits to their value, filler '&lt;' to 0; each character is
/// weighted by the repeating sequence 7, 3, 1 and the weighted sum mod 10 is the check digit.
/// </summary>
public static class Mrz731
{
    private static readonly int[] Weights = [7, 3, 1];

    public static int ComputeCheckDigit(string input)
    {
        var sum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            sum += CharValue(input[i]) * Weights[i % 3];
        }

        return sum % 10;
    }

    /// <summary>
    /// Validates a number whose final character is the embedded check digit.
    /// Returns false when the number is too short or the last character is not a digit.
    /// </summary>
    public static bool ValidateWithTrailingCheckDigit(string number)
    {
        var cleaned = new string(number.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleaned.Length < 2) return false;

        var checkChar = cleaned[^1];
        if (!char.IsDigit(checkChar)) return false;

        var expected = ComputeCheckDigit(cleaned[..^1]);
        return expected == (checkChar - '0');
    }

    private static int CharValue(char c)
    {
        if (c is >= '0' and <= '9') return c - '0';
        if (c is >= 'A' and <= 'Z') return c - 'A' + 10;
        if (c is >= 'a' and <= 'z') return c - 'a' + 10;
        return 0; // '<' filler and anything else
    }
}
