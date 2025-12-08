namespace PiggyzenMvp.API.DTOs;

public record CategoryDetailDto(
    int Id,
    int GroupId,
    string GroupKey,
    string GroupDisplayName,
    string Key,
    string DisplayName,
    string? UserDisplayName,
    bool IsSystemCategory,
    bool IsEnabled,
    int SortOrder
)
{
    public string EffectiveName => UserDisplayName ?? DisplayName;
}
