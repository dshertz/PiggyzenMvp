using System.Collections.Generic;

namespace PiggyzenMvp.API.Services.Config;

public sealed class ImportProfile
{
    public string[]? CandidateSeparators { get; set; }
    public string[]? DateFormats { get; set; }
    public Dictionary<string, string>? HeaderAliases { get; set; }
    public KindRuleDefinition[]? KindRules { get; set; }
    public ImportTransforms? Transforms { get; set; }
}
