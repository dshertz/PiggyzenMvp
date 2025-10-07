namespace PiggyzenMvp.Blazor.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentCategoryId { get; set; }
        public bool IsSystemCategory { get; set; }
    }
}
