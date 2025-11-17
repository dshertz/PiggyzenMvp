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
    private readonly NormalizeService _normalizeService;
    private readonly CategorizationService _categorizationService;

    public TransactionsController(
        PiggyzenMvpContext context,
        TransactionImportService importService,
        NormalizeService normalizeService,
        CategorizationService categorizationService
    )
    {
        _context = context;
        _importService = importService;
        _normalizeService = normalizeService;
        _categorizationService = categorizationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionListDto>>> GetAll(CancellationToken ct)
    {
        var items = await _context
            .Transactions.Include(t => t.Category)
            .ThenInclude(c => c.ParentCategory)
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
                CategoryName = t.Category != null ? t.Category.Name : null,

                TypeId =
                    t.Category == null
                        ? null
                        : (
                            t.Category.IsSystemCategory
                                ? t.Category.Id
                                : t.Category.ParentCategoryId
                        ),

                TypeName =
                    t.Category == null
                        ? null
                        : (
                            t.Category.IsSystemCategory ? t.Category.Name
                            : t.Category.ParentCategory != null ? t.Category.ParentCategory.Name
                            : null
                        ),
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("import")]
    [Consumes("text/plain")]
    public async Task<ActionResult<ImportResult>> ImportFromRawText([FromBody] string rawText)
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

        var parsed = _importService.ParseRawInput(rawText);
        var existingIds = await _context.Transactions.Select(t => t.ImportId).ToListAsync();

        foreach (var dto in parsed)
        {
            if (existingIds.Contains(dto.ImportId))
            {
                _importService.Errors.Add(
                    $"Rad {dto.SourceLineNumber}: Transaktionen finns redan – \"{dto.Description}\" {dto.TransactionDate:yyyy-MM-dd}"
                );
            }
        }

        var newTransactions = parsed
            .Where(dto => !existingIds.Contains(dto.ImportId))
            .Select(dto => new Transaction
            {
                BookingDate = dto.BookingDate,
                TransactionDate = dto.TransactionDate,
                Description = dto.Description,
                NormalizedDescription = _normalizeService.Normalize(dto.Description),
                Amount = dto.Amount,
                Balance = dto.Balance,
                ImportId = dto.ImportId,
            })
            .ToList();

        List<AutoCategorizeResult>? autoResults = null;

        if (newTransactions.Any())
        {
            await _context.Transactions.AddRangeAsync(newTransactions);
            await _context.SaveChangesAsync();

            var newIds = newTransactions.Select(t => t.Id).ToList();
            var autoCategorizeResults = await _categorizationService.AutoCategorizeBatchAsync(
                newIds
            );
            autoResults = autoCategorizeResults.ToList();
        }

        var importedDtos = newTransactions
            .Select(t => new TransactionDto
            {
                BookingDate = t.BookingDate,
                TransactionDate = t.TransactionDate,
                Description = t.Description,
                Amount = t.Amount,
                Balance = t.Balance,
                CategoryName = null,
            })
            .ToList();

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

        return Ok(
            new ImportResult
            {
                Transactions = importedDtos,
                Errors = _importService.Errors,
                AutoCategorized = autoCategorizedCount,
                AutoCategorizeErrors = autoCategorizeErrors,
            }
        );
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
}
