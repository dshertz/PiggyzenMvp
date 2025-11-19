namespace PiggyzenMvp.API.DTOs;

public class ImportResult
{
    public List<TransactionImportDto> ImportedTransactions { get; set; } = new();
    public List<string> ParsingErrors { get; set; } = new();
    public List<string> DuplicateWarnings { get; set; } = new();
    public int AutoCategorizedCount { get; set; }
    public List<AutoCategorizeErrorDto> AutoCategorizeErrors { get; set; } = new();
    public DateTime? ImportedAtUtc { get; set; }
}
