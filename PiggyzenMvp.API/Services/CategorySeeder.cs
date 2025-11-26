using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.Data.Seed;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services;

public class CategorySeeder
{
    private readonly PiggyzenMvpContext _context;

    public CategorySeeder(PiggyzenMvpContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _context.Database.MigrateAsync(ct);
        await SeedGroupsAsync(ct);
        await SeedCategoriesAsync(ct);
    }

    private async Task SeedGroupsAsync(CancellationToken ct)
    {
        var existing = await _context.CategoryGroups.ToListAsync(ct);
        var existingByKey = existing.ToDictionary(g => g.Key, g => g);

        foreach (var seed in CategorySeedData.Groups)
        {
            if (!existingByKey.TryGetValue(seed.Key, out var entity))
            {
                entity = new CategoryGroup
                {
                    Id = seed.Id,
                    Key = seed.Key,
                    DisplayName = seed.DisplayName,
                    SortOrder = seed.SortOrder,
                };
                _context.CategoryGroups.Add(entity);
            }
            else
            {
                if (entity.DisplayName != seed.DisplayName)
                    entity.DisplayName = seed.DisplayName;
                if (entity.SortOrder != seed.SortOrder)
                    entity.SortOrder = seed.SortOrder;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var existing = await _context
            .Categories.Where(c => c.IsSystemCategory)
            .ToListAsync(ct);
        var existingLookup = existing.ToDictionary(c => (c.GroupId, c.Key), c => c);

        foreach (var seed in CategorySeedData.Categories)
        {
            if (!existingLookup.TryGetValue((seed.GroupId, seed.Key), out var category))
            {
                category = new Category
                {
                    GroupId = seed.GroupId,
                    Key = seed.Key,
                    DisplayName = seed.DisplayName,
                    UserDisplayName = seed.DefaultUserDisplayName,
                    SortOrder = seed.SortOrder,
                    IsSystemCategory = true,
                    IsActive = true,
                    IsHidden = false,
                };
                _context.Categories.Add(category);
            }
            else
            {
                if (category.DisplayName != seed.DisplayName)
                    category.DisplayName = seed.DisplayName;
                if (category.SortOrder != seed.SortOrder)
                    category.SortOrder = seed.SortOrder;
                if (!category.IsActive)
                    category.IsActive = true;
                if (category.IsHidden)
                    category.IsHidden = false;
                if (
                    string.IsNullOrWhiteSpace(category.UserDisplayName)
                    && !string.IsNullOrWhiteSpace(seed.DefaultUserDisplayName)
                )
                {
                    category.UserDisplayName = seed.DefaultUserDisplayName;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
