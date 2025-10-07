namespace PiggyzenMvp.API.DTOs
{
    public class CategoryDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int? ParentCategoryId { get; init; }
        public bool IsSystemCategory { get; init; }
    }
}
