namespace PiggyzenMvp.Blazor.DTOs.Transactions;

public class ManualCategorizeResponse
{
    public int Categorized { get; set; }
    public List<ManualCategorizeError> Errors { get; set; } = new();
}

public class ManualCategorizeError
{
    public int TransactionId { get; set; }
    public string? Message { get; set; }
}
