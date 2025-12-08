namespace PiggyzenMvp.API.Models
{
    public class Category
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public CategoryGroup? Group { get; set; }
        public required string Key { get; set; }
        public required string DisplayName { get; set; }
        public string? UserDisplayName { get; set; }
        public bool IsSystemCategory { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
