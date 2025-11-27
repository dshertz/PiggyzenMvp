using System.Collections.Generic;

namespace PiggyzenMvp.Blazor.Services;

public enum DateFilterMode
{
    All,
    Yearly,
    Monthly
}

public class TransactionFilterState
{
    public string SearchText { get; set; } = string.Empty;
    public string FilterCategory { get; set; } = "all";
    public bool IsLoadedFromStorage { get; set; }

    public DateFilterMode DateMode { get; set; } = DateFilterMode.All;
    public bool IsDateMultiSelect { get; set; }
    public int? ActiveYear { get; set; }
    public int? ActiveMonth { get; set; }
    public List<int> SelectedYears { get; set; } = new();
    public List<string> SelectedYearMonths { get; set; } = new();
}
