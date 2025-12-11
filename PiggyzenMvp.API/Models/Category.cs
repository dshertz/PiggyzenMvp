using System.ComponentModel.DataAnnotations.Schema;

namespace PiggyzenMvp.API.Models
{
    public class Category
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public CategoryGroup? Group { get; set; }
        public required string Key { get; set; }
        public required string SystemDisplayName { get; set; }
        public string? CustomDisplayName { get; set; }
        public bool IsSystemCategory { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }

        [NotMapped]
        public string DisplayName => CustomDisplayName ?? SystemDisplayName;
    }
}
