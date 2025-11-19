namespace PiggyzenMvp.API.DTOs;

public class TransactionImportParseResult
{
    public List<TransactionImportDto> Transactions { get; } = new();
    public List<string> ParsingErrors { get; } = new();
}
