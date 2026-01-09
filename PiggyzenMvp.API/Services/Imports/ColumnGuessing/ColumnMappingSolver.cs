using System;
using System.Collections.Generic;
using System.Linq;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed class ColumnMappingSolver
{
    private const decimal DuplicateThreshold = 0.95m;
    private const int MaxCandidatesPerRole = 4;
    private const decimal DeterministicDateThreshold = 0.6m;

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

        var amountProfiles = activeProfiles.Where(profile => profile.AmountRate > 0m).ToList();
        var amountCandidatePool = BuildCandidatePool(amountProfiles, profile => amountScores[profile.Index]);
        var amountResolution = ResolveAmounts(amountProfiles, hasNegatives);
        var amountLoopCandidates = amountResolution != null
            ? new List<ColumnProfile?> { amountResolution.Amount }
            : amountCandidatePool;

        var reservedForAmount = new HashSet<int>();
        if (amountResolution != null)
        {
            reservedForAmount.Add(amountResolution.Amount.Index);
            if (amountResolution.Balance?.Index is int balanceIndex)
            {
                reservedForAmount.Add(balanceIndex);
            }
        }

        var dateResolutions = ResolveDeterministicDateResolutions(activeProfiles);
        if (!dateResolutions.Any())
        {
            dateResolutions = BuildFallbackDateResolutions(dateCandidates);
        }

        var hasDeterministicBalance = amountResolution?.Balance != null;
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

        foreach (var dateResolution in dateResolutions)
        {
            var dateScore = dateResolution.Score;
            var usedColumns = new HashSet<int>();
            if (dateResolution.Booking?.Index is int bookingIndex)
            {
                usedColumns.Add(bookingIndex);
            }

            if (dateResolution.Transaction?.Index is int transactionIndex)
            {
                usedColumns.Add(transactionIndex);
            }

            foreach (var typeCandidate in typeCandidates)
            {
                var typeIndex = typeCandidate?.Index;
                if (typeIndex.HasValue && (usedColumns.Contains(typeIndex.Value) || reservedForAmount.Contains(typeIndex.Value)))
                {
                    continue;
                }

                var typeScore = typeCandidate == null ? 0m : typeScores[typeCandidate.Index];
                var usedAfterType = new HashSet<int>(usedColumns);
                if (typeIndex.HasValue)
                {
                    usedAfterType.Add(typeIndex.Value);
                }

                foreach (var descriptionCandidate in descriptionCandidates)
                {
                    if (typeCandidate != null && descriptionCandidate != null
                        && typeCandidate.Index == descriptionCandidate.Index)
                    {
                        continue;
                    }

                    var descriptionIndex = descriptionCandidate?.Index;
                    if (descriptionIndex.HasValue
                        && (usedAfterType.Contains(descriptionIndex.Value) || reservedForAmount.Contains(descriptionIndex.Value)))
                    {
                        continue;
                    }

                    var descriptionScore = descriptionCandidate == null
                        ? 0m
                        : descriptionScores[descriptionCandidate.Index];

                    var usedAfterText = new HashSet<int>(usedAfterType);
                    if (descriptionIndex.HasValue)
                    {
                        usedAfterText.Add(descriptionIndex.Value);
                    }

                    foreach (var amountCandidate in amountLoopCandidates)
                    {
                        if (amountCandidate?.Index is int amountCandidateIndex && usedAfterText.Contains(amountCandidateIndex))
                        {
                            continue;
                        }

                        var amountScore = amountCandidate == null
                            ? 0m
                            : amountScores.GetValueOrDefault(amountCandidate.Index);

                        var usedAfterAmount = new HashSet<int>(usedAfterText);
                        if (amountCandidate?.Index is int amountCandidateIdx)
                        {
                            usedAfterAmount.Add(amountCandidateIdx);
                        }

                        ColumnProfile? balanceCandidate;
                        decimal balanceScore;

                        if (hasDeterministicBalance)
                        {
                            balanceCandidate = amountResolution!.Balance;
                            if (balanceCandidate?.Index is int balanceCandidateIndex && usedAfterAmount.Contains(balanceCandidateIndex))
                            {
                                balanceCandidate = null;
                            }

                            if (balanceCandidate?.Index is int balanceCandidateIdx)
                            {
                                usedAfterAmount.Add(balanceCandidateIdx);
                            }

                            balanceScore = balanceCandidate == null ? 0m : ComputeBalanceScore(balanceCandidate);
                        }
                        else
                        {
                            balanceCandidate = SelectBalanceCandidate(
                                amountCandidatePool,
                                usedAfterAmount,
                                amountCandidate?.Index,
                                amountScores);

                            balanceScore = balanceCandidate == null
                                ? 0m
                                : ComputeBalanceScore(balanceCandidate);

                            if (balanceCandidate?.Index is int balanceCandidateIndex)
                            {
                                usedAfterAmount.Add(balanceCandidateIndex);
                            }
                        }

                        var totalScore = dateScore + typeScore + descriptionScore + amountScore + balanceScore;

                        if (totalScore <= bestMap.TotalScore)
                        {
                            continue;
                        }

                        bestMap = new ImportColumnMap(
                            dateResolution.Booking?.Index,
                            dateResolution.Transaction?.Index,
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

    private static IReadOnlyList<DateResolution> ResolveDeterministicDateResolutions(
        IReadOnlyList<ColumnProfile> profiles)
    {
        var dateProfiles = profiles
            .Where(profile => profile.DateRate >= DeterministicDateThreshold)
            .OrderByDescending(profile => profile.DateRate)
            .ToList();

        if (dateProfiles.Count < 2)
        {
            return Array.Empty<DateResolution>();
        }

        DateResolution? best = null;
        foreach (var left in dateProfiles)
        {
            foreach (var right in dateProfiles)
            {
                if (left.Index == right.Index)
                {
                    continue;
                }

                var resolution = EvaluateDatePair(left, right);
                if (best == null || resolution.Score > best.Score)
                {
                    best = resolution;
                }
            }
        }

        return best == null ? Array.Empty<DateResolution>() : new[] { best };
    }

    private static DateResolution EvaluateDatePair(
        ColumnProfile left,
        ColumnProfile right)
    {
        var leftRatio = ComputeDateOrderingRatio(left, right);
        var rightRatio = ComputeDateOrderingRatio(right, left);
        var leftScore = ComputeDateScore(left, right);
        var rightScore = ComputeDateScore(right, left);

        ColumnProfile bookingProfile;
        ColumnProfile transactionProfile;
        decimal score;

        if (leftRatio > rightRatio || (leftRatio == rightRatio && leftScore >= rightScore))
        {
            bookingProfile = left;
            transactionProfile = right;
            score = leftScore;
        }
        else
        {
            bookingProfile = right;
            transactionProfile = left;
            score = rightScore;
        }

        if (AreDateColumnsIdentical(left, right))
        {
            return new DateResolution(left, left, score);
        }

        return new DateResolution(bookingProfile, transactionProfile, score);
    }

    private static decimal ComputeDateOrderingRatio(
        ColumnProfile bookingProfile,
        ColumnProfile transactionProfile)
    {
        var sampleCount = Math.Min(bookingProfile.DateSamples.Count, transactionProfile.DateSamples.Count);
        if (sampleCount == 0)
        {
            return 1m;
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

        return validPairs == 0 ? 1m : satisfying / (decimal)validPairs;
    }

    private static bool AreDateColumnsIdentical(ColumnProfile left, ColumnProfile right)
    {
        if (left.DateSamples.Count != right.DateSamples.Count)
        {
            return false;
        }

        for (var index = 0; index < left.DateSamples.Count; index++)
        {
            var leftValue = left.DateSamples[index];
            var rightValue = right.DateSamples[index];
            if (leftValue.HasValue != rightValue.HasValue)
            {
                return false;
            }

            if (leftValue.HasValue && leftValue.Value != rightValue.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<DateResolution> BuildFallbackDateResolutions(
        IReadOnlyList<ColumnProfile?> candidates)
    {
        var resolutions = new List<DateResolution>();
        foreach (var booking in candidates)
        {
            foreach (var transaction in candidates)
            {
                if (booking == null && transaction == null)
                {
                    continue;
                }

                var score = ComputeDateScore(booking, transaction);
                resolutions.Add(new DateResolution(booking, transaction, score));
            }
        }

        if (!resolutions.Any())
        {
            resolutions.Add(new DateResolution(null, null, 0m));
        }

        return resolutions;
    }

    private static AmountResolution? ResolveAmounts(
        IReadOnlyList<ColumnProfile> amountProfiles,
        bool hasNegatives)
    {
        var unique = RemoveDuplicateAmountProfiles(amountProfiles);
        if (unique.Count == 0)
        {
            return null;
        }

        if (unique.Count == 1)
        {
            return new AmountResolution(unique[0], null);
        }

        if (hasNegatives)
        {
            var amountProfile = unique
                .OrderByDescending(profile => profile.SignMixRate)
                .ThenBy(profile => profile.MedianAbs)
                .ThenByDescending(profile => profile.AmountRate)
                .First();

            var balanceProfile = unique
                .Where(profile => profile.Index != amountProfile.Index)
                .OrderByDescending(profile => profile.MostlyPositiveRate)
                .ThenByDescending(profile => profile.Median)
                .ThenByDescending(profile => profile.AmountRate)
                .FirstOrDefault();

            return new AmountResolution(amountProfile, balanceProfile);
        }

        var sorted = unique.OrderBy(profile => profile.Median).ToList();
        var amountCandidate = sorted.First();
        var balanceCandidate = sorted.Last();
        if (amountCandidate.Index == balanceCandidate.Index)
        {
            return new AmountResolution(amountCandidate, null);
        }

        return new AmountResolution(amountCandidate, balanceCandidate);
    }

    private static List<ColumnProfile> RemoveDuplicateAmountProfiles(IReadOnlyList<ColumnProfile> profiles)
    {
        var results = new List<ColumnProfile>();
        foreach (var profile in profiles)
        {
            if (results.Any(existing => AreAmountSamplesIdentical(existing, profile)))
            {
                continue;
            }

            results.Add(profile);
        }

        return results;
    }

    private static bool AreAmountSamplesIdentical(ColumnProfile left, ColumnProfile right)
    {
        if (left.AmountSamples.Count != right.AmountSamples.Count)
        {
            return false;
        }

        for (var index = 0; index < left.AmountSamples.Count; index++)
        {
            var leftValue = left.AmountSamples[index];
            var rightValue = right.AmountSamples[index];
            if (leftValue.HasValue != rightValue.HasValue)
            {
                return false;
            }

            if (leftValue.HasValue && leftValue.Value != rightValue.Value)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record DateResolution(ColumnProfile? Booking, ColumnProfile? Transaction, decimal Score);

    private sealed record AmountResolution(ColumnProfile Amount, ColumnProfile? Balance);
}
