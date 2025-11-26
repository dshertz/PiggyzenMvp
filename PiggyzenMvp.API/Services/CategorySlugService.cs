using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;

namespace PiggyzenMvp.API.Services;

public class CategorySlugService
{
    private static readonly Regex InvalidChars = new("[^a-z0-9\\-]+", RegexOptions.Compiled);
    private readonly PiggyzenMvpContext _context;

    public CategorySlugService(PiggyzenMvpContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueSlugAsync(
        int groupId,
        string source,
        CancellationToken ct = default
    )
    {
        var baseSlug = Slugify(source);
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = $"category-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        var slug = baseSlug;
        var counter = 2;

        while (
            await _context.Categories.AnyAsync(
                c => c.GroupId == groupId && c.Key == slug,
                ct
            )
        )
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace("å", "a", StringComparison.InvariantCulture)
            .Replace("ä", "a", StringComparison.InvariantCulture)
            .Replace("ö", "o", StringComparison.InvariantCulture);

        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                sb.Append('-');
            }
        }

        var slug = sb.ToString();
        slug = Regex.Replace(slug, "-{2,}", "-");
        slug = InvalidChars.Replace(slug, "");
        return slug.Trim('-');
    }
}
