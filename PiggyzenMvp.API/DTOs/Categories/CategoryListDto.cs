namespace PiggyzenMvp.API.DTOs;

public record CategoryListDto(int Id, string Name, int? ParentCategoryId, bool IsSystemCategory);
