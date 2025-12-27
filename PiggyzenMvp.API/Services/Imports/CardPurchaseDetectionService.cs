using System.Text.RegularExpressions;

namespace PiggyzenMvp.API.Services.Imports;

public sealed class CardPurchaseDetectionService
{
    private static readonly Regex CardNumberPattern = new(@"(?:\d{4}(?:[ -]?|$)){2,}", RegexOptions.Compiled);

    public bool IsCardPurchase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        if (trimmed.IndexOf("kort", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (CardNumberPattern.IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.Contains(',') && trimmed.Any(char.IsDigit))
        {
            return true;
        }

        return false;
    }
}
