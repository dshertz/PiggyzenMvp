namespace PiggyzenMvp.API.DTOs;

public class UpdateCategoryRequest
{
    public string? DisplayName { get; set; }
    public string? UserDisplayName { get; set; }
    public bool? IsHidden { get; set; }
    public bool? IsActive { get; set; }
}
