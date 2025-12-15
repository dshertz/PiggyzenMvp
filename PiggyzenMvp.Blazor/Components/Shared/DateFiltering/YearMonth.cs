using System;

namespace PiggyzenMvp.Blazor.Components.Shared.DateFiltering;

public readonly record struct YearMonth(int Year, int Month)
{
    public string ToStorageKey() => $"{Year:D4}-{Month:D2}";

    public static bool TryParse(string? input, out YearMonth period)
    {
        period = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (int.TryParse(parts[0], out var year) &&
            int.TryParse(parts[1], out var month) &&
            month is >= 1 and <= 12)
        {
            period = new YearMonth(year, month);
            return true;
        }

        return false;
    }
}
