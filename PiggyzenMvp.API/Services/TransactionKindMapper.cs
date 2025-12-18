using System.Collections.Concurrent;
using System.Text;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services;

public class TransactionKindMapper
{
    private static readonly (TransactionKind Kind, string[] Keywords)[] MappingRules = new[]
    {
        (
            TransactionKind.CardPurchase,
            new[] { NormalizeForMatching("Kortköp") }
        ),
        (
            TransactionKind.Swish,
            new[] { "swish" }
        ),
        (
            TransactionKind.Payment,
            new[]
            {
                NormalizeForMatching("Betalning"),
                NormalizeForMatching("Autogiro"),
            }
        ),
        (
            TransactionKind.Fee,
            new[]
            {
                NormalizeForMatching("Avg"),
                NormalizeForMatching("Årsavg"),
            }
        ),
        (
            TransactionKind.Transfer,
            new[] { NormalizeForMatching("Överföring") }
        ),
        (
            TransactionKind.Deposit,
            new[]
            {
                NormalizeForMatching("Insättning"),
                NormalizeForMatching("Kontantinsättning"),
            }
        ),
        (
            TransactionKind.LoanPayment,
            new[] { NormalizeForMatching("Låneinbetalning") }
        ),
        (TransactionKind.Interest, new[] { NormalizeForMatching("Ränta") }),
        (TransactionKind.Adjustment, new[] { NormalizeForMatching("Rabatt") }),
    };

    private readonly ConcurrentDictionary<string, int> _unknownCounts = new();
    private readonly ILogger<TransactionKindMapper> _logger;

    public TransactionKindMapper(ILogger<TransactionKindMapper> logger)
    {
        _logger = logger;
    }

    public TransactionKind Map(
        string? typeRaw,
        string normalizedDescription,
        string originalDescription,
        string importSource
    )
    {
        var normalizedTypeRaw = NormalizeForMatching(typeRaw);
        var normalizedDescriptionForMatching = NormalizeForMatching(normalizedDescription);
        var originalDescriptionForMatching = NormalizeForMatching(originalDescription);

        foreach (var (kind, keywords) in MappingRules)
        {
            if (ContainsKeyword(normalizedTypeRaw, keywords)
                || ContainsKeyword(normalizedDescriptionForMatching, keywords)
                || ContainsKeyword(originalDescriptionForMatching, keywords))
            {
                return kind;
            }
        }

        var sourceKey = string.IsNullOrWhiteSpace(importSource) ? "unknown" : importSource;
        var typeKey = string.IsNullOrWhiteSpace(typeRaw) ? "<empty>" : typeRaw.Trim();
        var counterKey = $"{sourceKey}|{typeKey}";
        var count = _unknownCounts.AddOrUpdate(counterKey, 1, (_, current) => current + 1);

        _logger.LogInformation(
            "Unknown transaction kind for import {ImportSource}. TypeRaw={TypeRaw}, Description=\"{Description}\", Count={Count}",
            sourceKey,
            typeKey,
            originalDescription,
            count
        );

        return TransactionKind.Unknown;
    }

    private static bool ContainsKeyword(string normalizedValue, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return false;

        foreach (var keyword in keywords)
        {
            if (normalizedValue.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizeForMatching(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.ToLowerInvariant();
        normalized = normalized.Replace("å", "a").Replace("ä", "a").Replace("ö", "o").Replace("é", "e");
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            builder.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return builder.ToString();
    }
}
