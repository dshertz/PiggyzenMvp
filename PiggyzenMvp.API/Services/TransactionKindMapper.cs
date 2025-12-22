using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PiggyzenMvp.API.Models;
using PiggyzenMvp.API.Services.Config;

namespace PiggyzenMvp.API.Services;

public class TransactionKindMapper
{
    private readonly ConcurrentDictionary<string, int> _unknownCounts = new();
    private readonly ILogger<TransactionKindMapper> _logger;
    private readonly EffectiveImportConfig _importConfig;

    public TransactionKindMapper(ILogger<TransactionKindMapper> logger, EffectiveImportConfig importConfig)
    {
        _logger = logger;
        _importConfig = importConfig;
    }

    public TransactionKind Map(
        string? typeRaw,
        string normalizedDescription,
        string originalDescription,
        string importSource
    )
    {
        var normalizedTypeRaw = ImportNormalization.NormalizeText(typeRaw);
        var normalizedDescriptionForMatching = ImportNormalization.NormalizeText(normalizedDescription);
        var originalDescriptionForMatching = ImportNormalization.NormalizeText(originalDescription);

        if (IsSwishType(normalizedTypeRaw))
        {
            return TransactionKind.Swish;
        }

        foreach (var rule in _importConfig.KindRules)
        {
            if (ContainsKeyword(normalizedTypeRaw, rule.Keywords)
                || ContainsKeyword(normalizedDescriptionForMatching, rule.Keywords)
                || ContainsKeyword(originalDescriptionForMatching, rule.Keywords))
            {
                return rule.Kind;
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

    private static bool ContainsKeyword(string normalizedValue, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return false;

        foreach (var keyword in keywords)
        {
            if (normalizedValue.Contains(keyword, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsSwishType(string normalizedTypeRaw)
    {
        return !string.IsNullOrWhiteSpace(normalizedTypeRaw)
            && normalizedTypeRaw.Contains("swish", StringComparison.Ordinal);
    }
}
