namespace PiggyzenMvp.Blazor.Services;

public class TransactionFilterState
{
    public string SearchText { get; set; } = string.Empty;
    public string FilterCategory { get; set; } = "all";
    public bool IsLoadedFromStorage { get; set; }
}
