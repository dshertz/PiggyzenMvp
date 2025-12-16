using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.DTOs;
using PiggyzenMvp.API.DTOs.Transactions;
using PiggyzenMvp.API.Models;
using PiggyzenMvp.API.Services;

namespace PiggyzenMvp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly PiggyzenMvpContext _context;
    private readonly TransactionImportService _importService;
    private readonly CategorizationService _categorizationService;

    public TransactionsController(
        PiggyzenMvpContext context,
        TransactionImportService importService,
        CategorizationService categorizationService
    )
    {
        _context = context;
        _importService = importService;
        _categorizationService = categorizationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionListDto>>> GetAll(CancellationToken ct)
    {
        var items = await _context
            .Transactions.Include(t => t.Category)
            .ThenInclude(c => c.Group)
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionListDto
            {
                Id = t.Id,
                BookingDate = t.BookingDate,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Amount = t.Amount,
                Balance = t.Balance,

                CategoryId = t.CategoryId,
                CategoryName = t.Category == null
                    ? null
                    : (t.Category.CustomDisplayName ?? t.Category.SystemDisplayName),

                TypeId = t.Category == null ? (int?)null : t.Category.GroupId,

                TypeName = t.Category == null
                    ? null
                    : (
                        t.Category.Group != null ? t.Category.Group.DisplayName : null
                    ),
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("import")]
    [Consumes("text/plain")]
    public async Task<ActionResult<ImportResult>> ImportFromRawText(
        [FromBody] string rawText,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return BadRequest(
                new ProblemDetails
                {
                    Title = "Importtext saknas",
                    Detail = "Ingen text skickades i förfrågan.",
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        var parseResult = _importService.ParseRawInput(rawText);
        var response = new ImportResult
        {
            ParsingErrors = parseResult.ParsingErrors.ToList(),
        };

        var parsedDtos = parseResult.Transactions;

        if (!parsedDtos.Any())
        {
            return Ok(response);
        }

        var dtoDates = parsedDtos.Select(dto => dto.TransactionDate.Date).Distinct().ToList();

        var existingImportIds = await _context.Transactions
            .Where(t => dtoDates.Contains(t.TransactionDate.Date))
            .Select(t => t.ImportId)
            .ToListAsync(ct);

        var existingIdSet = existingImportIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateWarnings = new List<string>();
        var newDtos = new List<TransactionImportDto>();

        foreach (var dto in parsedDtos)
        {
            if (existingIdSet.Contains(dto.ImportId))
            {
                duplicateWarnings.Add(
                    $"Rad {dto.SourceLineNumber}: transaktionen finns redan – \"{dto.Description}\" {dto.TransactionDate:yyyy-MM-dd}"
                );
                continue;
            }

            newDtos.Add(dto);
        }

        response.DuplicateWarnings = duplicateWarnings;

        if (!newDtos.Any())
        {
            response.DuplicateWarnings.Add("No new transactions were imported.");
            return Ok(response);
        }

        var importedAt = DateTime.UtcNow;
        var sequence = 1;
        var newTransactions = new List<Transaction>();

        foreach (var dto in newDtos)
        {
            newTransactions.Add(
                new Transaction
                {
                    BookingDate = dto.BookingDate,
                    TransactionDate = dto.TransactionDate,
                    Description = dto.Description,
                    NormalizedDescription = dto.NormalizedDescription,
                    Amount = dto.Amount,
                    Balance = dto.Balance,
                    ImportId = dto.ImportId,
                    ImportedAtUtc = importedAt,
                    ImportSequence = sequence++,
                    RawRow = dto.RawRow,
                }
            );
        }

        List<AutoCategorizeResult>? autoResults = null;

        if (newTransactions.Any())
        {
            await _context.Transactions.AddRangeAsync(newTransactions, ct);
            await _context.SaveChangesAsync(ct);

            var newIds = newTransactions.Select(t => t.Id).ToList();
            var autoCategorizeResults = await _categorizationService.AutoCategorizeBatchAsync(
                newIds
            );
            autoResults = autoCategorizeResults.ToList();
        }

        var autoCategorizedCount = autoResults?.Count(r => r.Error == null) ?? 0;
        var autoCategorizeErrors =
            autoResults
                ?.Where(r => r.Error != null)
                .Select(r => new AutoCategorizeErrorDto
                {
                    TransactionId = r.TransactionId,
                    Message = r.Error ?? string.Empty,
                })
                .ToList() ?? new List<AutoCategorizeErrorDto>();

        response.ImportedTransactions = newDtos;
        response.AutoCategorizedCount = autoCategorizedCount;
        response.AutoCategorizeErrors = autoCategorizeErrors;
        response.ImportedAtUtc = importedAt;

        return Ok(response);
    }

    [HttpGet("{id}/similar-uncategorized")]
    public async Task<ActionResult<List<SimilarTransactionDto>>> GetSimilarUncategorized(
        int id,
        CancellationToken ct
    )
    {
        var similar = await _categorizationService.GetSimilarUncategorizedAsync(id, ct);
        if (similar == null)
            return NotFound(new { Message = "Transaction not found." });

        return Ok(similar);
    }

    // Kanske begränsa antal transaktioner som kan kategoriseras i en request?
    [HttpPost("manual-categorize")]
    public async Task<IActionResult> ManualCategorize(
        [FromBody] IReadOnlyCollection<CategorizeRequest>? requests,
        CancellationToken ct
    )
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { Message = "Minst en transaktion måste skickas in." });

        var txIds = requests.Select(r => r.TransactionId).ToList();

        // 🔍 Kör validering i service
        var (ok, error, _) = await _categorizationService.ValidateSameSignAsync(txIds, ct);

        if (!ok)
            return BadRequest(new { Message = error });

        var categorized = 0;
        var autoCategorized = 0;
        var errors = new List<object>();

        foreach (var req in requests)
        {
            var result = await _categorizationService.CategorizeManuallyAsync(
                req.TransactionId,
                req.CategoryId,
                ct
            );

            if (result.Error != null)
            {
                errors.Add(new { req.TransactionId, Message = result.Error });
                continue;
            }

            categorized++;
            autoCategorized += result.AutoCategorized;
        }

        if (categorized == 0)
            return BadRequest(new { Message = "Inga transaktioner kategoriserades.", errors });

        return Ok(new { categorized, autoCategorized, errors });
    }

    [HttpPost("auto-categorize")]
    public async Task<IActionResult> AutoCategorize(
        [FromBody] IReadOnlyCollection<AutoCategorizeRequest>? requests,
        CancellationToken ct
    )
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { Message = "Minst en transaktion måste skickas in." });

        var transactionIds = requests.Select(r => r.TransactionId).ToList();
        var results = await _categorizationService.AutoCategorizeBatchAsync(transactionIds, ct);

        var processed = results.Count;
        var categorized = results.Count(r => r.Error == null);
        var errors = results
            .Where(r => r.Error != null)
            .Select(r => new { r.TransactionId, Message = r.Error })
            .ToList();

        if (categorized == 0)
            return BadRequest(new { Message = "Inga transaktioner auto-kategoriserades.", errors });

        return Ok(
            new
            {
                processed,
                categorized,
                errors,
            }
        );
    }

    [HttpPost("change-category")]
    public async Task<IActionResult> ChangeCategory(
        [FromBody] IReadOnlyCollection<ChangeCategoryRequest>? requests,
        CancellationToken ct
    )
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { Message = "Minst en transaktion måste skickas in." });

        var txIds = requests.Select(r => r.TransactionId).ToList();

        // 🔍 Kör validering i service
        var (ok, error, _) = await _categorizationService.ValidateSameSignAsync(txIds, ct);

        if (!ok)
            return BadRequest(new { Message = error });

        var updated = 0;
        var errors = new List<object>();

        foreach (var req in requests)
        {
            var err = await _categorizationService.ChangeCategoryAsync(
                req.TransactionId,
                req.CategoryId,
                ct
            );

            if (err != null)
            {
                errors.Add(new { req.TransactionId, Message = err });
                continue;
            }

            updated++;
        }

        if (updated == 0)
            return BadRequest(new { Message = "Inga transaktioner uppdaterades.", errors });

        return Ok(new { updated, errors });
    }

    [HttpPatch("metadata")]
    public async Task<IActionResult> UpdateMetadata(
        [FromBody] IReadOnlyCollection<TransactionMetadataUpdateDto>? requests,
        CancellationToken ct
    )
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { Message = "Minst en transaktion måste skickas in." });

        var transactionIds = requests.Select(r => r.TransactionId).ToList();
        var transactions = await _context.Transactions
            .Include(t => t.Tags)
            .Where(t => transactionIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var results = new List<TransactionMetadataUpdateResultDto>();
        var updated = 0;
        var hasChanges = false;

        foreach (var request in requests)
        {
            if (!transactions.TryGetValue(request.TransactionId, out var transaction))
            {
                results.Add(
                    new TransactionMetadataUpdateResultDto(
                        request.TransactionId,
                        false,
                        "Transaktionen hittades inte."
                    )
                );

                continue;
            }

            var transactionUpdated = false;

            if (request.Note is not null && transaction.Note != request.Note)
            {
                transaction.Note = request.Note;
                transactionUpdated = true;
            }

            var existingTagValues = new HashSet<string>(
                transaction.Tags.Select(t => t.Value),
                TagComparer
            );

            if (request.TagsSet != null)
            {
                var normalizedSet = NormalizeTagValues(request.TagsSet);
                var normalizedSetHash = new HashSet<string>(normalizedSet, TagComparer);

                if (!existingTagValues.SetEquals(normalizedSetHash))
                {
                    _context.TransactionTags.RemoveRange(transaction.Tags);
                    transaction.Tags.Clear();
                    existingTagValues.Clear();

                    foreach (var tagValue in normalizedSet)
                    {
                        var tag = new TransactionTag
                        {
                            TransactionId = transaction.Id,
                            Value = tagValue,
                        };

                        transaction.Tags.Add(tag);
                        existingTagValues.Add(tag.Value);
                    }

                    transactionUpdated = true;
                }
            }
            else
            {
                if (request.TagsAdd != null)
                {
                    var toAdd = NormalizeTagValues(request.TagsAdd);

                    foreach (var tagValue in toAdd)
                    {
                        if (existingTagValues.Add(tagValue))
                        {
                            transaction.Tags.Add(
                                new TransactionTag
                                {
                                    TransactionId = transaction.Id,
                                    Value = tagValue,
                                }
                            );
                            transactionUpdated = true;
                        }
                    }
                }

                if (request.TagsRemove != null)
                {
                    var toRemove = NormalizeTagValues(request.TagsRemove);

                    foreach (var tagValue in toRemove)
                    {
                        var tagEntity = transaction.Tags.FirstOrDefault(t =>
                            TagComparer.Equals(t.Value, tagValue));

                        if (tagEntity != null)
                        {
                            _context.TransactionTags.Remove(tagEntity);
                            transaction.Tags.Remove(tagEntity);
                            existingTagValues.Remove(tagValue);
                            transactionUpdated = true;
                        }
                    }
                }
            }

            if (transactionUpdated)
            {
                hasChanges = true;
                updated++;
            }

            results.Add(
                new TransactionMetadataUpdateResultDto(
                    request.TransactionId,
                    transactionUpdated,
                    null
                )
            );
        }

        if (hasChanges)
            await _context.SaveChangesAsync(ct);

        return Ok(new { updated, results });
    }

    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    // Trims and dedupes incoming tag names before comparison/storage.
    private static List<string> NormalizeTagValues(IReadOnlyCollection<string>? values)
    {
        if (values == null || values.Count == 0)
            return new List<string>();

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(trimmed => trimmed.Length > 0)
            .Distinct(TagComparer)
            .ToList();
    }
}
