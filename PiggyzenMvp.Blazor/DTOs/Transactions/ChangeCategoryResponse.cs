namespace PiggyzenMvp.Blazor.DTOs.Transactions;

public class ChangeCategoryResponse
{
    public int Updated { get; set; }
    public List<ManualCategorizeError> Errors { get; set; } = new();
}

