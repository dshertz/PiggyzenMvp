using System;
using System.Collections.Generic;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public enum ColumnType
{
    Unknown,
    Date,
    Amount,
    Text,
}

public sealed class ColumnProfile
{
    public ColumnProfile(
        int index,
        ColumnType type,
        int totalSamples,
        IReadOnlyList<string> samples,
        IReadOnlyList<DateTime?> dateSamples,
        IReadOnlyList<decimal?> amountSamples,
        decimal dateRate,
        decimal amountRate,
        decimal uniqueRate,
        decimal avgLength,
        int maxLength,
        decimal matchesTransactionTypeRate,
        decimal signatureMatchRate,
        decimal cardPurchaseRate,
        bool hasPositive,
        bool hasNegative,
        decimal signMixRate,
        decimal mostlyPositiveRate,
        decimal median,
        decimal medianAbs)
    {
        Index = index;
        Type = type;
        TotalSamples = totalSamples;
        Samples = samples;
        DateSamples = dateSamples;
        AmountSamples = amountSamples;
        DateRate = dateRate;
        AmountRate = amountRate;
        UniqueRate = uniqueRate;
        AvgLength = avgLength;
        MaxLength = maxLength;
        MatchesTransactionTypeRate = matchesTransactionTypeRate;
        SignatureMatchRate = signatureMatchRate;
        CardPurchaseRate = cardPurchaseRate;
        HasPositive = hasPositive;
        HasNegative = hasNegative;
        SignMixRate = signMixRate;
        MostlyPositiveRate = mostlyPositiveRate;
        Median = median;
        MedianAbs = medianAbs;
    }

    public int Index { get; }
    public ColumnType Type { get; }
    public int TotalSamples { get; }
    public IReadOnlyList<string> Samples { get; }
    public IReadOnlyList<DateTime?> DateSamples { get; }
    public IReadOnlyList<decimal?> AmountSamples { get; }
    public decimal DateRate { get; }
    public decimal AmountRate { get; }
    public decimal UniqueRate { get; }
    public decimal AvgLength { get; }
    public int MaxLength { get; }
    public decimal MatchesTransactionTypeRate { get; }
    public decimal SignatureMatchRate { get; }
    public decimal CardPurchaseRate { get; }
    public bool HasPositive { get; }
    public bool HasNegative { get; }
    public decimal SignMixRate { get; }
    public decimal MostlyPositiveRate { get; }
    public decimal Median { get; }
    public decimal MedianAbs { get; }
}
