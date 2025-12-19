using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.DTOs.DescriptionSignatures;
using PiggyzenMvp.API.Models;
using PiggyzenMvp.API.Services;

namespace PiggyzenMvp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DescriptionSignaturesController : ControllerBase
{
    private readonly PiggyzenMvpContext _context;
    private readonly DescriptionSignatureService _signatureService;
    private readonly CategorizationService _categorizationService;

    public DescriptionSignaturesController(
        PiggyzenMvpContext context,
        DescriptionSignatureService signatureService,
        CategorizationService categorizationService
    )
    {
        _context = context;
        _signatureService = signatureService;
        _categorizationService = categorizationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DescriptionSignatureDto>>> GetAllAsync(
        TransactionKind? kind,
        bool? isMachine,
        string? search,
        int? minSeenCount,
        int? limit,
        CancellationToken ct
    )
    {
        var query = _context.DescriptionSignatures.AsQueryable();

        if (kind.HasValue)
            query = query.Where(s => s.Kind == kind.Value);

        if (isMachine.HasValue)
            query = query.Where(s => s.IsMachineGenerated == isMachine.Value);

        if (minSeenCount.HasValue)
            query = query.Where(s => s.SeenCount >= minSeenCount.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.NormalizedDescription.Contains(normalized)
                || (s.MerchantCandidate != null && s.MerchantCandidate.ToLower().Contains(normalized))
            );
        }

        var max = limit.HasValue && limit.Value > 0 ? limit.Value : 200;

        var results = await query
            .OrderByDescending(s => s.LastSeen)
            .ThenByDescending(s => s.SeenCount)
            .Take(max)
            .Select(s => new DescriptionSignatureDto
            {
                Id = s.Id,
                NormalizedDescription = s.NormalizedDescription,
                Kind = s.Kind,
                IsPositive = s.IsPositive,
                IsMachineGenerated = s.IsMachineGenerated,
                MachineConfidence = s.MachineConfidence,
                MerchantCandidate = s.MerchantCandidate,
                Note = s.Note,
                MachineSource = s.MachineSource,
                SeenCount = s.SeenCount,
                FirstSeen = s.FirstSeen,
                LastSeen = s.LastSeen,
                AlgorithmVersion = s.AlgorithmVersion,
                TransactionCount = s.Transactions.Count,
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> PatchAsync(
        int id,
        [FromBody] DescriptionSignatureUpdateDto? dto,
        CancellationToken ct
    )
    {
        if (dto == null)
            return BadRequest();

        var signature = await _context.DescriptionSignatures.FindAsync(new object[] { id }, ct);
        if (signature == null)
            return NotFound();

        var manualOverride = false;

        if (dto.Kind.HasValue && dto.Kind.Value != signature.Kind)
        {
            signature.Kind = dto.Kind.Value;
            manualOverride = true;
        }

        if (dto.IsMachineGenerated.HasValue && dto.IsMachineGenerated.Value != signature.IsMachineGenerated)
        {
            signature.IsMachineGenerated = dto.IsMachineGenerated.Value;
            manualOverride = true;
        }

        if (dto.MachineConfidence.HasValue)
        {
            signature.MachineConfidence = Math.Max(0m, Math.Min(1m, dto.MachineConfidence.Value));
            manualOverride = true;
        }

        if (dto.MerchantCandidate != null)
        {
            signature.MerchantCandidate = dto.MerchantCandidate;
            manualOverride = true;
        }

        if (dto.Note != null)
        {
            signature.Note = dto.Note;
            manualOverride = true;
        }

        if (dto.MachineSource.HasValue)
        {
            signature.MachineSource = dto.MachineSource.Value;
        }
        else if (manualOverride)
        {
            signature.MachineSource = DescriptionSignatureSource.Manual;
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id}/apply-auto")]
    public async Task<IActionResult> ApplyAutoAsync(int id, CancellationToken ct)
    {
        var signature = await _context.DescriptionSignatures.FindAsync(new object[] { id }, ct);
        if (signature == null)
            return NotFound();

        var transactionIds = await _context.Transactions
            .Where(t => t.DescriptionSignatureId == id && t.CategoryId == null)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (transactionIds.Count == 0)
        {
            return Ok(new { AutoCategorized = 0, Errors = Array.Empty<object>() });
        }

        var results = await _categorizationService.AutoCategorizeBatchAsync(transactionIds, ct);
        var autoCount = results.Count(r => r.Error == null);
        var errors = results
            .Where(r => r.Error != null)
            .Select(r => new { r.TransactionId, r.Error })
            .ToList();

        return Ok(new { AutoCategorized = autoCount, Errors = errors });
    }

    [HttpPost("cleanup-orphans")]
    public async Task<ActionResult<object>> CleanupOrphansAsync(CancellationToken ct)
    {
        var removed = await _signatureService.CleanupOrphanedSignaturesAsync(ct);
        return Ok(new { Removed = removed });
    }
}
