namespace PiggyzenMvp.API.DTOs;

public class ImportResult
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
