using System.Linq;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services;

public class DescriptionSignatureService
{
    private const decimal AutoConfidenceThreshold = 0.75m;
    private const string AlgorithmVersion = "v1";

    private readonly PiggyzenMvpContext _context;
    private readonly NormalizeService _normalize;

    public DescriptionSignatureService(PiggyzenMvpContext context, NormalizeService normalize)
    {
        _context = context;
        _normalize = normalize;
    }

    public bool IsEligibleForAutoCategorization(DescriptionSignature? signature)
    {
        if (signature == null)
        {
            return false;
        }

        if (signature.IsMachineGenerated)
        {
            return true;
        }

        return signature.MachineConfidence >= AutoConfidenceThreshold;
    }

    public decimal GetAutoConfidenceThreshold() => AutoConfidenceThreshold;

    public async Task<DescriptionSignature> GetOrCreateAsync(
        string? normalizedDescription,
        TransactionKind kind,
        decimal amount,
        string rawDescription,
        CancellationToken ct = default
    )
    {
        var norm = string.IsNullOrWhiteSpace(normalizedDescription)
            ? _normalize.Normalize(rawDescription)
            : normalizedDescription;

        norm = _normalize.Normalize(norm);
        var isPositive = amount >= 0m;

        var signature = _context.DescriptionSignatures.Local.FirstOrDefault(
            s => s.NormalizedDescription == norm && s.Kind == kind && s.IsPositive == isPositive
        );

        if (signature == null)
        {
            signature = await _context.DescriptionSignatures.FirstOrDefaultAsync(
                s => s.NormalizedDescription == norm
                    && s.Kind == kind
                    && s.IsPositive == isPositive,
                ct
            );
        }

        var now = DateTime.UtcNow;
        var (isMachineGenerated, confidence) = AnalyzeDescription(rawDescription);
        var merchantCandidate = ExtractMerchantCandidate(norm, rawDescription);

        if (signature == null)
        {
            signature = new DescriptionSignature
            {
                NormalizedDescription = norm,
                Kind = kind,
                IsPositive = isPositive,
                IsMachineGenerated = isMachineGenerated,
                MachineConfidence = confidence,
                MerchantCandidate = merchantCandidate,
                MachineSource = DescriptionSignatureSource.Auto,
                SeenCount = 1,
                FirstSeen = now,
                LastSeen = now,
                AlgorithmVersion = AlgorithmVersion,
            };

            _context.DescriptionSignatures.Add(signature);
            return signature;
        }

        signature.SeenCount += 1;
        signature.LastSeen = now;

        if (signature.Kind != kind && signature.MachineSource == DescriptionSignatureSource.Auto)
        {
            signature.Kind = kind;
        }

        if (signature.MachineSource == DescriptionSignatureSource.Auto)
        {
            signature.IsMachineGenerated = isMachineGenerated;
            signature.MachineConfidence = confidence;
            if (string.IsNullOrWhiteSpace(signature.MerchantCandidate) && merchantCandidate != null)
            {
                signature.MerchantCandidate = merchantCandidate;
            }
            signature.AlgorithmVersion = AlgorithmVersion;
        }

        return signature;
    }

    public async Task<int> CleanupOrphanedSignaturesAsync(CancellationToken ct = default)
    {
        var orphanIds = await _context.DescriptionSignatures
            .Where(s => !_context.Transactions.Any(t => t.DescriptionSignatureId == s.Id))
            .Where(s => s.MachineSource == DescriptionSignatureSource.Auto && string.IsNullOrWhiteSpace(s.Note))
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (orphanIds.Count == 0)
        {
            return 0;
        }

        var orphans = await _context.DescriptionSignatures
            .Where(s => orphanIds.Contains(s.Id))
            .ToListAsync(ct);

        _context.DescriptionSignatures.RemoveRange(orphans);
        await _context.SaveChangesAsync(ct);

        return orphans.Count;
    }

    private static string? ExtractMerchantCandidate(string normalized, string raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var rawCandidate = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.Length >= 3);
            if (!string.IsNullOrWhiteSpace(rawCandidate))
            {
                return rawCandidate;
            }
        }

        var normalizedCandidate = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.Length >= 3);
        return normalizedCandidate;
    }

    private static (bool IsMachineGenerated, decimal Confidence) AnalyzeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return (false, 0m);
        }

        var trimmed = description.Trim();
        var verdicts = 0;

        if (IsAllCapsWithSpace(trimmed))
        {
            verdicts++;
        }

        if (HasCountryCodeSuffix(trimmed))
        {
            verdicts++;
        }

        if (trimmed.Contains(" AB"))
        {
            verdicts++;
        }

        var isMachine = verdicts > 0;
        var confidence = isMachine
            ? Math.Min(1m, 0.45m + verdicts * 0.2m)
            : 0.2m;

        return (isMachine, confidence);
    }

    private static bool IsAllCapsWithSpace(string text)
    {
        return text == text.ToUpperInvariant() && text.Contains(' ');
    }

    private static bool HasCountryCodeSuffix(string text)
    {
        return text.Length >= 3
            && text[^3] == ','
            && char.IsUpper(text[^2])
            && char.IsUpper(text[^1]);
    }
}
