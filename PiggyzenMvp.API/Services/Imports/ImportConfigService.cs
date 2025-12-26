using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services.Imports;

public sealed class ImportConfigService
{
    private const string DefaultProfileKey = "import.default.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly char[] FallbackSeparators = { '\t', ';', ',' };
    private static readonly string[] FallbackDateFormats =
    {
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImportConfigService> _logger;
    private readonly ConcurrentDictionary<string, Task<ResolvedImportConfig>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImportConfigService(
        IWebHostEnvironment environment,
        ILogger<ImportConfigService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task<ResolvedImportConfig> GetAsync(string? profileName)
    {
        var normalizedKey = NormalizeProfileKey(profileName);
        return _cache.GetOrAdd(normalizedKey, _ => Task.FromResult(BuildResolvedConfig(normalizedKey)));
    }

    private ResolvedImportConfig BuildResolvedConfig(string profileKey)
    {
        var configRoot = GetConfigRootPath();
        var sections = new List<ImportSource>
        {
            new(
                DefaultProfileKey,
                LoadProfile(
                    Path.Combine(configRoot, DefaultProfileKey),
                    DefaultProfileKey,
                    MinimalDefaultProfile()))
        };

        if (string.Equals(profileKey, DefaultProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var bankProfile in EnumerateBankProfiles(configRoot))
            {
                sections.Add(new(bankProfile.SourceName, LoadProfile(bankProfile.Path, bankProfile.SourceName, new ImportProfile())));
            }
        }
        else
        {
            var profilePath = ResolveProfilePath(profileKey, configRoot);
            sections.Add(new(profileKey, LoadProfile(profilePath, profileKey, new ImportProfile())));
        }

        return MergeSections(sections);
    }

    private static string NormalizeProfileKey(string? profileName)
    {
        var candidate = string.IsNullOrWhiteSpace(profileName)
            ? DefaultProfileKey
            : profileName.Trim();

        candidate = candidate.Replace('\\', '/');
        while (candidate.StartsWith("./", StringComparison.Ordinal))
        {
            candidate = candidate[2..];
        }

        if (candidate.Length == 0)
        {
            return DefaultProfileKey;
        }

        if (!candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            candidate += ".json";
        }

        var segments = candidate
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .ToArray();

        if (segments.Length == 0)
        {
            return DefaultProfileKey;
        }

        return string.Join('/', segments);
    }

    private static string ResolveProfilePath(string normalizedKey, string configRoot)
    {
        var segments = normalizedKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return Path.Combine(configRoot, DefaultProfileKey);
        }

        var parts = new string[segments.Length + 1];
        parts[0] = configRoot;
        for (var i = 0; i < segments.Length; i++)
        {
            parts[i + 1] = segments[i];
        }

        return Path.Combine(parts);
    }

    private string GetConfigRootPath()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
        return Path.Combine(repoRoot, "Config", "Import");
    }

    private IEnumerable<(string SourceName, string Path)> EnumerateBankProfiles(string configRoot)
    {
        var banksRoot = Path.Combine(configRoot, "banks");
        if (!Directory.Exists(banksRoot))
        {
            _logger.LogInformation("Banks directory {BanksRoot} was not found, skipping bank overrides", banksRoot);
            yield break;
        }

        var bankFiles = Directory.GetFiles(banksRoot, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var bankFile in bankFiles)
        {
            var sourceName = Path.GetRelativePath(configRoot, bankFile);
            yield return (sourceName, bankFile);
        }
    }

    private ImportProfile LoadProfile(string path, string sourceName, ImportProfile fallback)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Config {Source} not found at {Path}; using fallback profile", sourceName, path);
            return fallback;
        }

        try
        {
            var content = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<ImportProfile>(content, JsonOptions);
            if (profile == null)
            {
                _logger.LogWarning("Config {Source} could not be parsed; using fallback profile", sourceName);
                return fallback;
            }

            return profile;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Config {Source} could not be loaded; using fallback profile", sourceName);
            return fallback;
        }
    }

    private ResolvedImportConfig MergeSections(IReadOnlyList<ImportSource> sections)
    {
        var separators = new HashSet<char>();
        var dateFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var headerAliases = new Dictionary<string, HeaderField>(StringComparer.Ordinal);
        var keywordMapping = new Dictionary<string, TransactionKind>(StringComparer.Ordinal);
        var orderedKeywordEntries = new List<(TransactionKind Kind, string Keyword)>();
        var transforms = new ImportTransforms();

        foreach (var section in sections)
        {
            MergeSeparators(section.Profile, separators);
            MergeDateFormats(section.Profile, dateFormats);
            MergeHeaderAliases(section.Profile, section.SourceName, headerAliases);
            MergeKindRules(section.Profile, section.SourceName, keywordMapping, orderedKeywordEntries);
            MergeTransforms(section.Profile, transforms);
        }

        EnsureFallbacks(separators, dateFormats);

        var kindRules = orderedKeywordEntries
            .GroupBy(entry => entry.Kind)
            .Select(group => new KindRule(group.Key, group.Select(entry => entry.Keyword).ToList()))
            .ToList();

        return new ResolvedImportConfig(
            separators.ToList(),
            dateFormats.ToList(),
            headerAliases,
            kindRules,
            transforms);
    }

    private void MergeSeparators(ImportProfile profile, HashSet<char> separators)
    {
        if (profile.CandidateSeparators == null)
        {
            return;
        }

        foreach (var candidate in profile.CandidateSeparators)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            separators.Add(candidate[0]);
        }
    }

    private void MergeDateFormats(ImportProfile profile, HashSet<string> dateFormats)
    {
        if (profile.DateFormats == null)
        {
            return;
        }

        foreach (var format in profile.DateFormats)
        {
            var trimmed = format?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            dateFormats.Add(trimmed);
        }
    }

    private void MergeHeaderAliases(
        ImportProfile profile,
        string sourceName,
        Dictionary<string, HeaderField> headerAliases)
    {
        if (profile.HeaderAliases == null)
        {
            return;
        }

        foreach (var alias in profile.HeaderAliases)
        {
            if (string.IsNullOrWhiteSpace(alias.Key))
            {
                continue;
            }

            var normalizedKey = ImportNormalization.NormalizeHeader(alias.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            var target = alias.Value;
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!Enum.TryParse<HeaderField>(target, true, out var field))
            {
                _logger.LogWarning(
                    "Header alias '{Header}' in {Source} targets unknown field '{Target}'",
                    alias.Key,
                    sourceName,
                    target);
                continue;
            }

            if (headerAliases.TryGetValue(normalizedKey, out var existingField))
            {
                if (existingField != field)
                {
                    _logger.LogWarning(
                        "Header alias '{Header}' in {Source} maps to {Field} but alias is already mapped to {ExistingField}",
                        alias.Key,
                        sourceName,
                        field,
                        existingField);
                }

                continue;
            }

            headerAliases[normalizedKey] = field;
        }
    }

    private void MergeKindRules(
        ImportProfile profile,
        string sourceName,
        Dictionary<string, TransactionKind> keywordMapping,
        List<(TransactionKind Kind, string Keyword)> orderedKeywordEntries)
    {
        if (profile.KindRules == null)
        {
            return;
        }

        foreach (var rule in profile.KindRules)
        {
            if (string.IsNullOrWhiteSpace(rule.Kind))
            {
                continue;
            }

            if (!Enum.TryParse<TransactionKind>(rule.Kind, true, out var kind))
            {
                _logger.LogWarning("Kind rule in {Source} references unknown kind '{Kind}'", sourceName, rule.Kind);
                continue;
            }

            foreach (var keyword in rule.Keywords ?? Array.Empty<string>())
            {
                var normalizedKeyword = ImportNormalization.NormalizeText(keyword);
                if (string.IsNullOrWhiteSpace(normalizedKeyword))
                {
                    continue;
                }

                if (keywordMapping.TryGetValue(normalizedKeyword, out var existingKind))
                {
                    if (existingKind != kind)
                    {
                        _logger.LogWarning(
                            "Keyword '{Keyword}' in {Source} maps to {Kind} but already assigned to {ExistingKind}; ignoring duplicate",
                            keyword,
                            sourceName,
                            kind,
                            existingKind);
                    }

                    continue;
                }

                keywordMapping[normalizedKeyword] = kind;
                orderedKeywordEntries.Add((kind, normalizedKeyword));
            }
        }
    }

    private void MergeTransforms(ImportProfile profile, ImportTransforms transforms)
    {
        if (profile.Transforms == null)
        {
            return;
        }

        if (profile.Transforms.SwishCopyTypeToDescriptionWhenEmpty)
        {
            transforms.SwishCopyTypeToDescriptionWhenEmpty = true;
        }
    }

    private void EnsureFallbacks(HashSet<char> separators, HashSet<string> dateFormats)
    {
        if (!separators.Any())
        {
            _logger.LogWarning("No separators defined after merging configs; falling back to defaults");
            foreach (var separator in FallbackSeparators)
            {
                separators.Add(separator);
            }
        }

        if (!dateFormats.Any())
        {
            _logger.LogWarning("No date formats defined after merging configs; falling back to defaults");
            foreach (var format in FallbackDateFormats)
            {
                dateFormats.Add(format);
            }
        }
    }

    private static ImportProfile MinimalDefaultProfile()
    {
        return new ImportProfile
        {
            CandidateSeparators = new[] { "\t", ";", "," },
            DateFormats = new[]
            {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "dd-MM-yyyy",
                "dd/MM/yyyy",
                "MM-dd-yyyy",
                "MM/dd/yyyy",
            }
        };
    }

    private sealed record ImportSource(string SourceName, ImportProfile Profile);

    public sealed class ResolvedImportConfig
    {
        public ResolvedImportConfig(
            IReadOnlyList<char> candidateSeparators,
            IReadOnlyList<string> dateFormats,
            IReadOnlyDictionary<string, HeaderField> headerAliasesNormalized,
            IReadOnlyList<KindRule> kindRules,
            ImportTransforms transforms)
        {
            CandidateSeparators = candidateSeparators ?? throw new ArgumentNullException(nameof(candidateSeparators));
            DateFormats = dateFormats ?? throw new ArgumentNullException(nameof(dateFormats));
            HeaderAliasesNormalized = headerAliasesNormalized ?? throw new ArgumentNullException(nameof(headerAliasesNormalized));
            KindRules = kindRules ?? throw new ArgumentNullException(nameof(kindRules));
            Transforms = transforms ?? throw new ArgumentNullException(nameof(transforms));
        }

        public IReadOnlyList<char> CandidateSeparators { get; }
        public IReadOnlyList<string> DateFormats { get; }
        public IReadOnlyDictionary<string, HeaderField> HeaderAliasesNormalized { get; }
        public IReadOnlyList<KindRule> KindRules { get; }
        public ImportTransforms Transforms { get; }
    }

    public sealed class ImportProfile
    {
        public string[]? CandidateSeparators { get; set; }
        public string[]? DateFormats { get; set; }
        public Dictionary<string, string>? HeaderAliases { get; set; }
        public KindRuleDefinition[]? KindRules { get; set; }
        public ImportTransforms? Transforms { get; set; }
    }

    public sealed class KindRuleDefinition
    {
        public string Kind { get; set; } = string.Empty;
        public string[]? Keywords { get; set; }
    }

    public sealed class KindRule
    {
        public KindRule(TransactionKind kind, IReadOnlyList<string> keywords)
        {
            Kind = kind;
            Keywords = keywords;
        }

        public TransactionKind Kind { get; }
        public IReadOnlyList<string> Keywords { get; }
    }

    public sealed class ImportTransforms
    {
        public bool SwishCopyTypeToDescriptionWhenEmpty { get; set; }
    }
}
