namespace PiggyzenMvp.API.DTOs;

public class UpdateCategoryRequest
{
    public string? DisplayName { get; set; }
    public string? UserDisplayName { get; set; }
    public bool? IsEnabled { get; set; }
}
