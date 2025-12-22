using System.Text;

namespace PiggyzenMvp.API.Services;

internal static class ImportNormalization
{
    public static string NormalizeHeader(string? input) => Normalize(input, allowWhitespace: false);

    public static string NormalizeText(string? input) => Normalize(input, allowWhitespace: true);

    private static string Normalize(string? input, bool allowWhitespace)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.ToLowerInvariant();
        normalized = normalized.Replace("å", "a").Replace("ä", "a").Replace("ö", "o");

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (allowWhitespace && char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
            else if (allowWhitespace)
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }
}
