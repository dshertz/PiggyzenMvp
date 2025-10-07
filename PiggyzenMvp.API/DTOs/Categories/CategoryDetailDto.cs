namespace PiggyzenMvp.API.DTOs;

public record CategoryDetailDto(int Id, string Name, int? ParentCategoryId, bool IsSystemCategory);
