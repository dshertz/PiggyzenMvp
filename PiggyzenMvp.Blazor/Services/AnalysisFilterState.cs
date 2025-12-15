using System.Collections.Generic;

namespace PiggyzenMvp.Blazor.Services;

public class AnalysisFilterState
{
    public DateFilterMode DateMode { get; set; } = DateFilterMode.Monthly;
    public bool IsDateMultiSelect { get; set; }
    public int? ActiveYear { get; set; }
    public int? ActiveMonth { get; set; }
    public List<int> SelectedYears { get; set; } = new();
    public List<string> SelectedYearMonths { get; set; } = new();
    public int? SelectedGroupId { get; set; }
    public int? SelectedCategoryId { get; set; }
}
