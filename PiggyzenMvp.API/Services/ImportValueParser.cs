using System.Globalization;
using System.Linq;

namespace PiggyzenMvp.API.Services;

internal static class ImportValueParser
{
    public static bool TryParseDate(string input, IReadOnlyList<string> formats, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(input) || formats == null || formats.Count == 0)
        {
            return false;
        }

        var trimmed = input.Trim();
        if (DateTime.TryParseExact(
            trimmed,
            formats.ToArray(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate))
        {
            date = parsedDate;
            return true;
        }

        return false;
    }

    public static bool TryParseAmount(string input, out decimal? amount)
    {
        amount = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var normalized = cleaned.Replace(",", ".");

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            amount = parsed;
            return true;
        }

        return false;
    }
}
