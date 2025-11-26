namespace PiggyzenMvp.API.DTOs;

public record CategoryGroupDto(
    int Id,
    string Key,
    string DisplayName,
    int SortOrder,
    IReadOnlyCollection<CategoryDto> Categories
);
