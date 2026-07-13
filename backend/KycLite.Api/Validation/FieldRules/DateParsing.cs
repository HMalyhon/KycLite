using System.Globalization;
using System.Text.RegularExpressions;

namespace KycLite.Api.Validation.FieldRules;

/// <summary>
/// Parses field values into dates and resolves a date-rule parameter, which may be an absolute
/// ISO date (2030-01-01) or a relative expression: "today" optionally followed by an offset such
/// as "today-18y", "today+30d", "today-6m". The relative form lets the default checks reproduce
/// the old age (DOB on/before today-18y) and expiry (on/after today) rules.
/// </summary>
public static partial class DateParsing
{
    private static readonly string[] ValueFormats =
        ["yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy"];

    public static bool TryParseValue(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return DateOnly.TryParseExact(value, ValueFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static bool TryResolveReference(string? param, DateOnly today, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(param)) return false;

        var trimmed = param.Trim();

        var match = RelativeExpression().Match(trimmed);
        if (match.Success)
        {
            date = today;
            if (match.Groups["sign"].Success)
            {
                // The regex bounds the offset to digits only, but not its magnitude: a crafted
                // param like "today+99999999999d" overflows int, and "today+9999y" pushes past
                // DateOnly's year-9999 ceiling. Treat either as an unresolvable reference (a
                // graceful rule failure) rather than letting the exception bubble up as a 500.
                if (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
                    return false;
                if (match.Groups["sign"].Value == "-") amount = -amount;

                try
                {
                    date = match.Groups["unit"].Value switch
                    {
                        "y" => today.AddYears(amount),
                        "m" => today.AddMonths(amount),
                        _ => today.AddDays(amount),
                    };
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            return true;
        }

        return DateOnly.TryParseExact(trimmed, ValueFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    [GeneratedRegex(@"^today\s*((?<sign>[+-])\s*(?<amount>\d+)\s*(?<unit>[ymd]))?$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeExpression();
}
