using System;
using System.Collections.Generic;
using System.Linq;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed class ColumnMappingSolver
{
    private const decimal DuplicateThreshold = 0.95m;
    private const int MaxCandidatesPerRole = 4;

    public ImportColumnMap Solve(ColumnProfilingResult profiling)
    {
        if (profiling == null)
        {
            throw new ArgumentNullException(nameof(profiling));
        }

        var redundantColumns = ResolveRedundantColumns(profiling.Profiles);
        var redundantList = redundantColumns.OrderBy(index => index).ToList();

        var activeProfiles = profiling.Profiles
            .Where(profile => !redundantColumns.Contains(profile.Index))
            .ToList();

        if (!activeProfiles.Any())
        {
            return new ImportColumnMap(
                null,
                null,
                null,
                null,
                null,
                null,
                0m,
                0m,
                0m,
                0m,
                0m,
                0m,
                redundantList);
        }

        var hasNegatives = profiling.Profiles.Any(profile => profile.HasNegative);
        var typeScores = activeProfiles.ToDictionary(profile => profile.Index, ComputeTransactionTypeScore);
        var descriptionScores = activeProfiles.ToDictionary(profile => profile.Index, ComputeDescriptionScore);
        var amountScores = activeProfiles.ToDictionary(profile => profile.Index, profile => ComputeAmountScore(profile, hasNegatives));

        var dateCandidates = BuildCandidatePool(activeProfiles, profile => profile.DateRate);
        var typeCandidates = BuildCandidatePool(activeProfiles, profile => typeScores[profile.Index]);
        var descriptionCandidates = BuildCandidatePool(activeProfiles, profile => descriptionScores[profile.Index]);
        var amountCandidates = BuildCandidatePool(activeProfiles.Where(profile => profile.AmountRate > 0m), profile => amountScores[profile.Index]);

        var bestMap = new ImportColumnMap(
            null,
            null,
            null,
            null,
            null,
            null,
            decimal.MinValue,
            0m,
            0m,
            0m,
            0m,
            0m,
            redundantList);

        foreach (var bookingCandidate in dateCandidates)
        {
            foreach (var transactionCandidate in dateCandidates)
            {
                if (bookingCandidate == null && transactionCandidate == null)
                {
                    continue;
                }

                var dateScore = ComputeDateScore(bookingCandidate, transactionCandidate);
                var usedColumns = new HashSet<int>();

                if (bookingCandidate?.Index is int bookingIndex)
                {
                    usedColumns.Add(bookingIndex);
                }

                if (transactionCandidate?.Index is int transactionIndex
                    && transactionCandidate.Index != bookingCandidate?.Index)
                {
                    usedColumns.Add(transactionIndex);
                }

                foreach (var typeCandidate in typeCandidates)
                {
                    var typeIndex = typeCandidate?.Index;
                    if (typeIndex.HasValue && usedColumns.Contains(typeIndex.Value))
                    {
                        continue;
                    }

                    var typeScore = typeCandidate == null ? 0m : typeScores[typeCandidate.Index];

                    foreach (var descriptionCandidate in descriptionCandidates)
                    {
                        if (typeCandidate != null && descriptionCandidate != null
                            && typeCandidate.Index == descriptionCandidate.Index)
                        {
                            continue;
                        }

                        if (descriptionCandidate?.Index is int descriptionIndex
                            && usedColumns.Contains(descriptionIndex))
                        {
                            continue;
                        }

                        var descriptionScore = descriptionCandidate == null
                            ? 0m
                            : descriptionScores[descriptionCandidate.Index];

                        var usedAfterText = new HashSet<int>(usedColumns);
                        if (typeIndex.HasValue)
                        {
                            usedAfterText.Add(typeIndex.Value);
                        }

                        if (descriptionCandidate?.Index is int descIdx)
                        {
                            usedAfterText.Add(descIdx);
                        }

                        foreach (var amountCandidate in amountCandidates)
                        {
                            if (amountCandidate?.Index is int amountIdx && usedAfterText.Contains(amountIdx))
                            {
                                continue;
                            }

                            var amountScore = amountCandidate == null
                                ? 0m
                                : amountScores.GetValueOrDefault(amountCandidate.Index);

                            var usedAfterAmount = new HashSet<int>(usedAfterText);
                            if (amountCandidate?.Index is int amtIdx)
                            {
                                usedAfterAmount.Add(amtIdx);
                            }

                            var balanceCandidate = SelectBalanceCandidate(
                                amountCandidates,
                                usedAfterAmount,
                                amountCandidate?.Index,
                                amountScores);

                            var balanceScore = balanceCandidate == null
                                ? 0m
                                : ComputeBalanceScore(balanceCandidate);

                            var totalScore = dateScore + typeScore + descriptionScore + amountScore + balanceScore;

                            if (totalScore <= bestMap.TotalScore)
                            {
                                continue;
                            }

                            bestMap = new ImportColumnMap(
                                bookingCandidate?.Index,
                                transactionCandidate?.Index,
                                typeCandidate?.Index,
                                descriptionCandidate?.Index,
                                amountCandidate?.Index,
                                balanceCandidate?.Index,
                                totalScore,
                                dateScore,
                                typeScore,
                                descriptionScore,
                                amountScore,
                                balanceScore,
                                redundantList);
                        }
                    }
                }
            }
        }

        return bestMap;
    }

    private static List<ColumnProfile?> BuildCandidatePool(
        IEnumerable<ColumnProfile> profiles,
        Func<ColumnProfile, decimal> scoreSelector)
    {
        var pool = profiles
            .OrderByDescending(scoreSelector)
            .Take(MaxCandidatesPerRole)
            .Cast<ColumnProfile?>()
            .ToList();

        if (!pool.Contains(null))
        {
            pool.Add(null);
        }

        return pool;
    }

    private static HashSet<int> ResolveRedundantColumns(IReadOnlyList<ColumnProfile> profiles)
    {
        var redundant = new HashSet<int>();
        for (var i = 0; i < profiles.Count; i++)
        {
            var left = profiles[i];
            if (redundant.Contains(left.Index))
            {
                continue;
            }

            for (var j = i + 1; j < profiles.Count; j++)
            {
                var right = profiles[j];
                if (redundant.Contains(right.Index))
                {
                    continue;
                }

                var duplicateRate = ComputeDuplicateRate(left, right);
                if (duplicateRate >= DuplicateThreshold)
                {
                    redundant.Add(right.Index);
                }
            }
        }

        return redundant;
    }

    private static decimal ComputeDuplicateRate(ColumnProfile left, ColumnProfile right)
    {
        var sampleCount = Math.Min(left.Samples.Count, right.Samples.Count);
        if (sampleCount == 0)
        {
            return 0m;
        }

        var matches = 0;
        var comparisons = 0;

        for (var index = 0; index < sampleCount; index++)
        {
            var leftValue = left.Samples[index];
            var rightValue = right.Samples[index];
            if (string.IsNullOrWhiteSpace(leftValue) && string.IsNullOrWhiteSpace(rightValue))
            {
                continue;
            }

            comparisons++;
            if (string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        return comparisons == 0 ? 0m : matches / (decimal)comparisons;
    }

    private static decimal ComputeDateScore(ColumnProfile? booking, ColumnProfile? transaction)
    {
        if (booking == null && transaction == null)
        {
            return 0m;
        }

        var bookingProfile = booking ?? transaction!;
        var transactionProfile = transaction ?? booking!;

        var sampleCount = Math.Min(bookingProfile.DateSamples.Count, transactionProfile.DateSamples.Count);
        if (sampleCount == 0)
        {
            return (bookingProfile.DateRate + transactionProfile.DateRate) / 2m * 0.6m;
        }

        var validPairs = 0;
        var satisfying = 0;

        for (var index = 0; index < sampleCount; index++)
        {
            var bookingValue = bookingProfile.DateSamples[index];
            var transactionValue = transactionProfile.DateSamples[index];
            if (!bookingValue.HasValue || !transactionValue.HasValue)
            {
                continue;
            }

            validPairs++;
            if (bookingValue.Value >= transactionValue.Value)
            {
                satisfying++;
            }
        }

        var ratio = validPairs == 0 ? 1m : satisfying / (decimal)validPairs;
        var dateConfidence = (bookingProfile.DateRate + transactionProfile.DateRate) / 2m;
        return dateConfidence * 0.6m + ratio * 0.4m;
    }

    private static decimal ComputeTransactionTypeScore(ColumnProfile profile)
    {
        var normalizedLength = Math.Max(0m, 1m - Math.Min(1m, profile.AvgLength / 30m));
        var uniqueness = 1m - Math.Min(1m, profile.UniqueRate);
        return profile.MatchesTransactionTypeRate * 0.7m + uniqueness * 0.2m + normalizedLength * 0.1m;
    }

    private static decimal ComputeDescriptionScore(ColumnProfile profile)
    {
        var lengthBoost = Math.Min(1m, profile.AvgLength / 40m);
        return profile.SignatureMatchRate * 0.4m
            + profile.CardPurchaseRate * 0.3m
            + lengthBoost * 0.2m
            + profile.UniqueRate * 0.1m;
    }

    private static decimal ComputeAmountScore(ColumnProfile profile, bool hasNegatives)
    {
        var medianNormalized = Math.Min(1m, Math.Abs(profile.Median) / 10000m);
        var medianScore = 1m - medianNormalized;
        var baseRate = profile.AmountRate;

        if (hasNegatives)
        {
            return baseRate * 0.5m + profile.SignMixRate * 0.4m + medianScore * 0.1m;
        }

        return baseRate * 0.6m + medianScore * 0.4m;
    }

    private static decimal ComputeBalanceScore(ColumnProfile profile)
    {
        var medianNormalized = Math.Min(1m, Math.Abs(profile.Median) / 10000m);
        return profile.AmountRate * 0.5m + medianNormalized * 0.2m;
    }

    private static ColumnProfile? SelectBalanceCandidate(
        IReadOnlyList<ColumnProfile?> amountCandidates,
        HashSet<int> usedColumns,
        int? amountIndex,
        IReadOnlyDictionary<int, decimal> amountScores)
    {
        ColumnProfile? best = null;
        var bestScore = decimal.MinValue;

        foreach (var candidate in amountCandidates)
        {
            if (candidate == null)
            {
                continue;
            }

            if (amountIndex.HasValue && candidate.Index == amountIndex.Value)
            {
                continue;
            }

            if (usedColumns.Contains(candidate.Index))
            {
                continue;
            }

            var score = amountScores.GetValueOrDefault(candidate.Index);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }
}
