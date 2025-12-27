using System;
using System.Collections.Generic;
using System.Linq;
using PiggyzenMvp.API.Services.Imports;
using ResolvedImportConfig = PiggyzenMvp.API.Services.Imports.ImportConfigService.ResolvedImportConfig;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed class ColumnProfiler
{
    private const int MaxSampleRows = 20;

    private readonly ResolvedImportConfig _importConfig;
    private readonly DescriptionSignatureService _signatureService;
    private readonly CardPurchaseDetectionService _cardPurchaseDetection;
    private readonly IReadOnlyList<string> _typeKeywords;

    public ColumnProfiler(
        ResolvedImportConfig importConfig,
        DescriptionSignatureService signatureService,
        CardPurchaseDetectionService cardPurchaseDetection)
    {
        _importConfig = importConfig ?? throw new ArgumentNullException(nameof(importConfig));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _cardPurchaseDetection = cardPurchaseDetection ?? throw new ArgumentNullException(nameof(cardPurchaseDetection));
        _typeKeywords = importConfig.KindRules
            .SelectMany(rule => rule.Keywords)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .ToList();
    }

    public ColumnProfilingResult Profile(
        IReadOnlyList<ImportSampleRow> sampleRows,
        int columnCount)
    {
        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        var rowsForProfiling = sampleRows.Take(MaxSampleRows).ToList();
        var builders = Enumerable.Range(0, columnCount)
            .Select(index => new ColumnProfileBuilder(index, _cardPurchaseDetection, _typeKeywords, _importConfig.DateFormats))
            .ToArray();

        foreach (var row in rowsForProfiling)
        {
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = columnIndex < row.Columns.Length ? row.Columns[columnIndex] : string.Empty;
                builders[columnIndex].AddSample(cell);
            }
        }

        var normalizedCandidates = builders
            .SelectMany(builder => builder.NormalizedSamples)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        IReadOnlySet<string> signatureMatches = normalizedCandidates.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : _signatureService.GetMatchingNormalizedDescriptions(normalizedCandidates);

        var profiles = builders
            .Select(builder => builder.Build(signatureMatches))
            .ToList();

        return new ColumnProfilingResult(profiles, rowsForProfiling);
    }

    private sealed class ColumnProfileBuilder
    {
        private readonly int _index;
        private readonly CardPurchaseDetectionService _cardPurchaseDetection;
        private readonly IReadOnlyList<string> _typeKeywords;
        private readonly IReadOnlyList<string> _dateFormats;

        private readonly List<string> _samples = new();
        private readonly List<string> _normalizedSamples = new();
        private readonly List<DateTime?> _dates = new();
        private readonly List<decimal?> _amounts = new();

        private int _maxLength;
        private int _totalLength;
        private int _positiveCount;
        private int _negativeCount;
        private int _dateMatches;
        private int _amountMatches;
        private int _matchesTransactionType;
        private int _cardPurchaseMatches;

        public ColumnProfileBuilder(
            int index,
            CardPurchaseDetectionService cardPurchaseDetection,
            IReadOnlyList<string> typeKeywords,
            IReadOnlyList<string> dateFormats)
        {
            _index = index;
            _cardPurchaseDetection = cardPurchaseDetection;
            _typeKeywords = typeKeywords ?? Array.Empty<string>();
            _dateFormats = dateFormats ?? Array.Empty<string>();
            _maxLength = 0;
            _totalLength = 0;
        }

        public IReadOnlyList<string> NormalizedSamples => _normalizedSamples;

        public void AddSample(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            _samples.Add(trimmed);

            var normalized = ImportNormalization.NormalizeText(trimmed);
            _normalizedSamples.Add(normalized);

            _maxLength = Math.Max(_maxLength, trimmed.Length);
            _totalLength += trimmed.Length;

            if (ImportValueParser.TryParseDate(trimmed, _dateFormats, out var parsedDate))
            {
                _dates.Add(parsedDate);
                _dateMatches++;
            }
            else
            {
                _dates.Add(null);
            }

            if (ImportValueParser.TryParseAmount(trimmed, out var parsedAmount))
            {
                _amounts.Add(parsedAmount);
                _amountMatches++;
                if (parsedAmount > 0)
                {
                    _positiveCount++;
                }

                if (parsedAmount < 0)
                {
                    _negativeCount++;
                }
            }
            else
            {
                _amounts.Add(null);
            }

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                foreach (var keyword in _typeKeywords)
                {
                    if (normalized.Contains(keyword, StringComparison.Ordinal))
                    {
                        _matchesTransactionType++;
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(trimmed)
                && _cardPurchaseDetection.IsCardPurchase(trimmed))
            {
                _cardPurchaseMatches++;
            }
        }

        public ColumnProfile Build(IReadOnlySet<string> signatureMatches)
        {
            var sampleCount = _samples.Count;
            var denominator = Math.Max(1, sampleCount);

            var uniqueNormalized = _normalizedSamples
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Count();

            var signatureCount = _normalizedSamples.Count(
                value => !string.IsNullOrWhiteSpace(value)
                         && signatureMatches.Contains(value));

            var dateRate = _dateMatches / (decimal)denominator;
            var amountRate = _amountMatches / (decimal)denominator;
            var uniqueRate = uniqueNormalized / (decimal)denominator;
            var avgLength = sampleCount == 0 ? 0m : _totalLength / (decimal)sampleCount;
            var matchesTypeRate = _matchesTransactionType / (decimal)denominator;
            var signatureRate = signatureCount / (decimal)denominator;
            var cardPurchaseRate = _cardPurchaseMatches / (decimal)denominator;

            var positiveNegativeTotal = _positiveCount + _negativeCount;
            var signMixRate = positiveNegativeTotal == 0
                ? 0m
                : Math.Min(_positiveCount, _negativeCount) / (decimal)positiveNegativeTotal;
            var mostlyPositiveRate = positiveNegativeTotal == 0
                ? 0m
                : _positiveCount / (decimal)positiveNegativeTotal;

            var median = ComputeMedian(_amounts.Where(value => value.HasValue).Select(value => value!.Value));
            var medianAbs = ComputeMedian(_amounts.Where(value => value.HasValue).Select(value => Math.Abs(value!.Value)));

            var textRate = Math.Max(0m, 1m - Math.Max(dateRate, amountRate));
            var highestRate = Math.Max(dateRate, Math.Max(amountRate, textRate));
            var type = highestRate == dateRate
                ? ColumnType.Date
                : highestRate == amountRate
                    ? ColumnType.Amount
                    : ColumnType.Text;

            return new ColumnProfile(
                _index,
                type,
                sampleCount,
                _samples.ToList(),
                _dates.ToList(),
                _amounts.ToList(),
                dateRate,
                amountRate,
                uniqueRate,
                avgLength,
                _maxLength,
                matchesTypeRate,
                signatureRate,
                cardPurchaseRate,
                _positiveCount > 0,
                _negativeCount > 0,
                signMixRate,
                mostlyPositiveRate,
                median,
                medianAbs
            );
        }

        private static decimal ComputeMedian(IEnumerable<decimal> values)
        {
            var snapshot = values.OrderBy(value => value).ToList();
            if (snapshot.Count == 0)
            {
                return 0m;
            }

            var midpoint = snapshot.Count / 2;
            if (snapshot.Count % 2 == 1)
            {
                return snapshot[midpoint];
            }

            return (snapshot[midpoint - 1] + snapshot[midpoint]) / 2m;
        }
    }
}
