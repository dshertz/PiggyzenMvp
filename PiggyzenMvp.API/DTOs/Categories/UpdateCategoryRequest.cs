namespace PiggyzenMvp.API.DTOs;

public class UpdateCategoryRequest
{
    public string? SystemDisplayName { get; set; }
    public string? CustomDisplayName { get; set; }
    public bool? IsEnabled { get; set; }
}
