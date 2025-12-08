using System.Collections.Generic;

namespace PiggyzenMvp.Blazor.DTOs;

public class CategoryGroupDto
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int SortOrder { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string GroupKey { get; set; } = "";
    public string GroupDisplayName { get; set; } = "";
    public string Key { get; set; } = "";
    public string SystemDisplayName { get; set; } = "";
    public string? CustomDisplayName { get; set; }
    public bool IsSystemCategory { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(CustomDisplayName)
            ? SystemDisplayName
            : CustomDisplayName!;

    public string EffectiveName => DisplayName;
}
