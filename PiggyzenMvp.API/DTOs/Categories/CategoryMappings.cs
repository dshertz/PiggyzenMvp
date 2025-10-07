using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.DTOs;

public static class CategoryMappings
{
    public static CategoryListDto ToListDto(this Category c) =>
        new(c.Id, c.Name, c.ParentCategoryId, c.IsSystemCategory);

    public static CategoryDetailDto ToDetailDto(this Category c) =>
        new(c.Id, c.Name, c.ParentCategoryId, c.IsSystemCategory);

    public static Category ToEntity(this CreateCategoryRequest req) =>
        new() { Name = req.Name.Trim(), ParentCategoryId = req.ParentCategoryId };
}
