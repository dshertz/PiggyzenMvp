namespace PiggyzenMvp.API.Models
{
    public class CategoryGroup
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string DisplayName { get; set; }
        public int SortOrder { get; set; }
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}
