using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.DTOs;
using PiggyzenMvp.API.Services;

namespace PiggyzenMvp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly PiggyzenMvpContext _context;
        private readonly CategorySlugService _slugService;

        public CategoriesController(PiggyzenMvpContext context, CategorySlugService slugService)
        {
            _context = context;
            _slugService = slugService;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryGroupDto>>> GetAll(CancellationToken ct)
        {
            var groups = await _context
                .CategoryGroups.Include(g => g.Categories)
                .OrderBy(g => g.SortOrder)
                .ToListAsync(ct);

            var dtos = groups
                .Select(group =>
                {
                    var categories = group.Categories
                        .OrderBy(c => c.SortOrder)
                        .ThenBy(c => c.DisplayName)
                        .Select(c =>
                        {
                            c.Group = group;
                            return c.ToDto();
                        })
                        .ToList();

                    return group.ToGroupDto(categories);
                })
                .ToList();

            return Ok(dtos);
        }

        // GET: api/categories/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CategoryDetailDto>> GetById(int id, CancellationToken ct)
        {
            var c = await _context
                .Categories.Include(c => c.Group)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            return c is null ? NotFound() : Ok(c.ToDetailDto());
        }

        // POST: api/categories
        [HttpPost]
        public async Task<ActionResult<CategoryDetailDto>> Create(
            [FromBody] CreateCategoryRequest dto,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(dto.DisplayName))
                return BadRequest(new { Message = "DisplayName is required." });

            var group = await _context.CategoryGroups.FirstOrDefaultAsync(
                g => g.Id == dto.GroupId,
                ct
            );
            if (group is null)
                return BadRequest(new { Message = $"Group {dto.GroupId} not found." });

            var slug = await _slugService.GenerateUniqueSlugAsync(
                dto.GroupId,
                dto.DisplayName,
                ct
            );
            var nextSort =
                await _context.Categories.Where(c => c.GroupId == dto.GroupId).MaxAsync(
                    c => (int?)c.SortOrder,
                    ct
                ) ?? 0;

            var entity = dto.ToEntity(slug, nextSort + 1);
            _context.Categories.Add(entity);
            await _context.SaveChangesAsync(ct);
            await _context.Entry(entity).Reference(c => c.Group).LoadAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity.ToDetailDto());
        }

        // PUT: api/categories/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateCategoryRequest dto,
            CancellationToken ct = default
        )
        {
            var c = await _context
                .Categories.Include(x => x.Group)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                if (c.IsSystemCategory)
                    return BadRequest(new { Message = "System categories cannot change DisplayName." });

                c.DisplayName = dto.DisplayName.Trim();
            }

            if (dto.UserDisplayName != null)
            {
                var trimmed = dto.UserDisplayName.Trim();
                c.UserDisplayName = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            }

            if (dto.IsHidden.HasValue)
                c.IsHidden = dto.IsHidden.Value;

            if (dto.IsActive.HasValue)
            {
                if (c.IsSystemCategory && dto.IsActive == false)
                    return BadRequest(new { Message = "System categories cannot be deactivated." });

                c.IsActive = dto.IsActive.Value;
            }

            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        // DELETE: api/categories/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var c = await _context.Categories.FindAsync(new object[] { id }, ct);
            if (c is null)
                return NotFound();
            if (c.IsSystemCategory)
                return BadRequest(new { Message = "Cannot delete system category." });

            var inUse =
                await _context.Transactions.AnyAsync(t => t.CategoryId == id, ct)
                || await _context.CategorizationRules.AnyAsync(h => h.CategoryId == id, ct);
            if (inUse)
                return BadRequest(
                    new
                    {
                        Message =
                            "Kategorin används av transaktioner eller regler. Flytta dessa poster till en annan kategori innan du försöker ta bort den.",
                    }
                );

            _context.Categories.Remove(c);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
