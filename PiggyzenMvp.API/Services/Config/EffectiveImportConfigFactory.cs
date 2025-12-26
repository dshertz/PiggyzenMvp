using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services.Config;

public sealed class EffectiveImportConfigFactory
{
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
        "MM/dd/yyyy"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EffectiveImportConfigFactory> _logger;

    public EffectiveImportConfigFactory(
        IWebHostEnvironment environment,
        ILogger<EffectiveImportConfigFactory> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public EffectiveImportConfig Create()
    {
        var definitions = LoadDefinitions();
        return BuildEffectiveConfig(definitions);
    }

    private IReadOnlyList<ImportSection> LoadDefinitions()
    {
        var sections = new List<ImportSection>();
        var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
        var configRoot = Path.Combine(repoRoot, "Config", "Import");
        var defaultPath = Path.Combine(configRoot, "import.default.json");
        sections.Add(
            new ImportSection(
                "import.default.json",
                LoadProfile(defaultPath, "import.default.json", MinimalDefaultProfile())));

        var banksRoot = Path.Combine(configRoot, "banks");
        if (Directory.Exists(banksRoot))
        {
            var bankFiles = Directory.GetFiles(banksRoot, "*.json")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

            foreach (var bankFile in bankFiles)
            {
                var sourceName = Path.GetRelativePath(configRoot, bankFile);
                var profile = LoadProfile(bankFile, sourceName, new ImportProfile());
                sections.Add(new ImportSection(sourceName, profile));
            }
        }
        else
        {
            _logger.LogInformation("Banks directory {BanksRoot} was not found, skipping bank overrides", banksRoot);
        }

        return sections;
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

    private EffectiveImportConfig BuildEffectiveConfig(IReadOnlyList<ImportSection> sections)
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

        return new EffectiveImportConfig(
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
                _logger.LogWarning("Header alias '{Header}' in {Source} targets unknown field '{Target}'", alias.Key, sourceName, target);
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
                        existingField
                    );
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
                            existingKind
                        );
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

    private void EnsureFallbacks(
        HashSet<char> separators,
        HashSet<string> dateFormats)
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

    private sealed record ImportSection(string SourceName, ImportProfile Profile);
}
