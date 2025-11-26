namespace PiggyzenMvp.API.DTOs;

public class CreateCategoryRequest
{
    public required string DisplayName { get; set; }
    public int GroupId { get; set; }
}
