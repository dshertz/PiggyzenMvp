namespace PiggyzenMvp.API.DTOs;

public class CreateCategoryRequest
{
    public required string SystemDisplayName { get; set; }
    public int GroupId { get; set; }
}
