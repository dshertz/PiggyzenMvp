namespace PiggyzenMvp.API.DTOs;

public record CategoryDetailDto(
    int Id,
    int GroupId,
    string GroupKey,
    string GroupDisplayName,
    string Key,
    string SystemDisplayName,
    string? CustomDisplayName,
    bool IsSystemCategory,
    bool IsEnabled,
    int SortOrder
)
{
    public string DisplayName => CustomDisplayName ?? SystemDisplayName;
}
