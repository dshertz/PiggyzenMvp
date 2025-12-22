using System;
using System.Collections.Generic;

namespace PiggyzenMvp.API.Services.Config;

public sealed class EffectiveImportConfig
{
    public EffectiveImportConfig(
        IReadOnlyList<char> candidateSeparators,
        IReadOnlyList<string> dateFormats,
        IReadOnlyDictionary<string, HeaderField> headerAliasesNormalized,
        IReadOnlyList<string> headerIndicatorTokens,
        IReadOnlyList<string> typeIndicatorTokens,
        IReadOnlyList<KindRule> kindRules)
    {
        CandidateSeparators = candidateSeparators ?? throw new ArgumentNullException(nameof(candidateSeparators));
        DateFormats = dateFormats ?? throw new ArgumentNullException(nameof(dateFormats));
        HeaderAliasesNormalized = headerAliasesNormalized ?? throw new ArgumentNullException(nameof(headerAliasesNormalized));
        HeaderIndicatorTokens = headerIndicatorTokens ?? throw new ArgumentNullException(nameof(headerIndicatorTokens));
        TypeIndicatorTokens = typeIndicatorTokens ?? throw new ArgumentNullException(nameof(typeIndicatorTokens));
        KindRules = kindRules ?? throw new ArgumentNullException(nameof(kindRules));
    }

    public IReadOnlyList<char> CandidateSeparators { get; }
    public IReadOnlyList<string> DateFormats { get; }
    public IReadOnlyDictionary<string, HeaderField> HeaderAliasesNormalized { get; }
    public IReadOnlyList<string> HeaderIndicatorTokens { get; }
    public IReadOnlyList<string> TypeIndicatorTokens { get; }
    public IReadOnlyList<KindRule> KindRules { get; }
}
