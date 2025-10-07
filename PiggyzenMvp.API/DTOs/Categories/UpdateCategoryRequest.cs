namespace PiggyzenMvp.API.DTOs;

public class UpdateCategoryRequest
{
    public required string Name { get; set; }
    public int? ParentCategoryId { get; set; }
}
