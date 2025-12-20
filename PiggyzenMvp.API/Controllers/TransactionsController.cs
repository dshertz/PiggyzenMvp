using System.Data;
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
    private readonly TransactionKindMapper _kindMapper;
    private readonly DescriptionSignatureService _descriptionSignatureService;

    public TransactionsController(
        PiggyzenMvpContext context,
        TransactionImportService importService,
        CategorizationService categorizationService,
        TransactionKindMapper kindMapper,
        DescriptionSignatureService descriptionSignatureService
    )
    {
        _context = context;
        _importService = importService;
        _categorizationService = categorizationService;
        _kindMapper = kindMapper;
        _descriptionSignatureService = descriptionSignatureService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionListDto>>> GetAll(CancellationToken ct)
    {
            var items = await _context
                .Transactions.Include(t => t.Category)
                .ThenInclude(c => c!.Group)
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionListDto
            {
                Id = t.Id,
                BookingDate = t.BookingDate,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Amount = t.Amount,
                Balance = t.Balance,
                Note = t.Note,

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
        return Ok(await ImportParsedTransactionsAsync(parseResult, ct));
    }

    [HttpPost("import/preview")]
    [Consumes("text/plain")]
    public ActionResult<TransactionImportPreviewResult> PreviewImport([FromBody] string rawText)
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

        var preview = _importService.PreparePreview(rawText);
        return Ok(preview);
    }

    [HttpPost("import/schema")]
    public async Task<ActionResult<ImportResult>> ImportWithSchema(
        [FromBody] TransactionImportWithSchemaRequest? request,
        CancellationToken ct
    )
    {
        if (request == null || request.Schema == null || string.IsNullOrWhiteSpace(request.RawText))
        {
            return BadRequest(
                new ProblemDetails
                {
                    Title = "Schema eller text saknas",
                    Detail = "Både texten och ett schema måste skickas.",
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        var parseResult = _importService.ParseWithSchema(request.RawText, request.Schema);
        return Ok(await ImportParsedTransactionsAsync(parseResult, ct));
    }

    private async Task<ImportResult> ImportParsedTransactionsAsync(
        TransactionImportParseResult parseResult,
        CancellationToken ct
    )
    {
        var response = new ImportResult
        {
            ParsingErrors = parseResult.ParsingErrors.ToList(),
        };

        var parsedDtos = parseResult.Transactions;

        if (!parsedDtos.Any())
        {
            return response;
        }

        var groupedByFingerprint = parsedDtos
            .GroupBy(
                dto => new TransactionFingerprint(
                    dto.TransactionDate.Date,
                    dto.NormalizedDescription,
                    dto.Amount
                )
            )
            .ToList();

        var candidateDates = groupedByFingerprint
            .Select(group => group.Key.Date)
            .Distinct()
            .ToList();

        var duplicateWarnings = new List<string>();
        var newDtos = new List<TransactionImportDto>();

        await using var dbTransaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var existingTransactions = await _context.Transactions
            .Where(t => candidateDates.Contains(t.TransactionDate.Date))
            .ToListAsync(ct);

        var existingFingerprintCounts = existingTransactions
            .GroupBy(
                t => new TransactionFingerprint(
                    t.TransactionDate.Date,
                    t.NormalizedDescription,
                    t.Amount
                )
            )
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var group in groupedByFingerprint)
        {
            var fingerprint = group.Key;
            var alreadyImported =
                existingFingerprintCounts.TryGetValue(fingerprint, out var count) ? count : 0;

            if (alreadyImported > 0)
            {
                var sample = group.First();
                duplicateWarnings.Add(
                    $"Rad {sample.SourceLineNumber}: transaktionstypen \"{sample.Description}\" finns redan för {fingerprint.Date:yyyy-MM-dd} och {fingerprint.Amount:F2}, hela gruppen blockeras."
                );
                continue;
            }

            var ordinalBase = alreadyImported;
            var index = 0;

            foreach (var dto in group)
            {
                var ordinal = ordinalBase + index + 1;
                dto.ImportId = BuildImportId(dto, ordinal);
                newDtos.Add(dto);
                index++;
            }

            existingFingerprintCounts[fingerprint] = ordinalBase + index;
        }

        response.DuplicateWarnings = duplicateWarnings;

        if (!newDtos.Any())
        {
            response.DuplicateWarnings.Add("Ingen ny transaktion importerades.");
            await dbTransaction.CommitAsync(ct);
            return response;
        }

        var importedAt = DateTime.UtcNow;
        var sequence = 1;
        var newTransactions = new List<Transaction>();

        foreach (var dto in newDtos)
        {
            var kind = _kindMapper.Map(
                dto.TypeRaw,
                dto.NormalizedDescription,
                dto.Description,
                dto.ImportId
            );

            var signature = await _descriptionSignatureService.GetOrCreateAsync(
                dto.NormalizedDescription,
                kind,
                dto.Amount,
                dto.Description,
                ct
            );

            newTransactions.Add(
                new Transaction
                {
                    BookingDate = dto.BookingDate,
                    TransactionDate = dto.TransactionDate,
                    Description = dto.Description,
                    NormalizedDescription = dto.NormalizedDescription,
                    Amount = dto.Amount,
                    Balance = dto.Balance,
                    TypeRaw = dto.TypeRaw,
                    Kind = kind,
                    ImportId = dto.ImportId,
                    ImportedAtUtc = importedAt,
                    ImportSequence = sequence++,
                    RawRow = dto.RawRow,
                    DescriptionSignature = signature,
                }
            );
        }

        List<AutoCategorizeResult>? autoResults = null;

        if (newTransactions.Any())
        {
            await _context.Transactions.AddRangeAsync(newTransactions, ct);
            await _context.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            var newIds = newTransactions.Select(t => t.Id).ToList();
            var autoCategorizeResults = await _categorizationService.AutoCategorizeBatchAsync(
                newIds,
                ct
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

        return response;
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

    private static string BuildImportId(TransactionImportDto dto, int ordinal)
    {
        return $"{dto.TransactionDate:yyyy-MM-dd}|{dto.NormalizedDescription}|{dto.Amount:F2}|{ordinal}";
    }

    private readonly record struct TransactionFingerprint(
        DateTime Date,
        string NormalizedDescription,
        decimal Amount
    );
}
