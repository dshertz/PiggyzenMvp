using System.Collections.Generic;
using System.Linq;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.DTOs;

public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) =>
        new(
            category.Id,
            category.GroupId,
            category.Group?.Key ?? string.Empty,
            category.Group?.DisplayName ?? string.Empty,
            category.Key,
            category.SystemDisplayName,
            category.CustomDisplayName,
            category.IsSystemCategory,
            category.IsEnabled,
            category.SortOrder
        );

    public static CategoryDetailDto ToDetailDto(this Category category) =>
        new(
            category.Id,
            category.GroupId,
            category.Group?.Key ?? string.Empty,
            category.Group?.DisplayName ?? string.Empty,
            category.Key,
            category.SystemDisplayName,
            category.CustomDisplayName,
            category.IsSystemCategory,
            category.IsEnabled,
            category.SortOrder
        );

    public static CategoryGroupDto ToGroupDto(this CategoryGroup group, IEnumerable<CategoryDto> categories) =>
        new(group.Id, group.Key, group.DisplayName, group.SortOrder, categories.ToList());

    public static Category ToEntity(this CreateCategoryRequest req, string key, int sortOrder) =>
        new()
        {
            GroupId = req.GroupId,
            Key = key,
            SystemDisplayName = req.SystemDisplayName.Trim(),
            CustomDisplayName = null,
            SortOrder = sortOrder,
            IsSystemCategory = false,
            IsEnabled = true,
        };
}
