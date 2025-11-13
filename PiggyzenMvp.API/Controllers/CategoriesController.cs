using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.DTOs;

namespace PiggyzenMvp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly PiggyzenMvpContext _context;

        public CategoriesController(PiggyzenMvpContext context) => _context = context;

        // GET: api/categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryListDto>>> GetAll(
            CancellationToken ct
        ) =>
            Ok(
                await _context
                    .Categories.OrderBy(c => c.ParentCategoryId)
                    .ThenBy(c => c.Name)
                    .Select(c => new CategoryListDto(
                        c.Id,
                        c.Name,
                        c.ParentCategoryId,
                        c.IsSystemCategory
                    ))
                    .ToListAsync(ct)
            );

        // GET: api/categories/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CategoryDetailDto>> GetById(int id, CancellationToken ct)
        {
            var c = await _context.Categories.FindAsync(new object[] { id }, ct);
            return c is null ? NotFound() : Ok(c.ToDetailDto());
        }

        // POST: api/categories
        // Body: { "name": "...", "parentCategoryId": 1 }
        // Regel: parent krävs och måste vara en systemkategori
        [HttpPost]
        public async Task<ActionResult<CategoryDetailDto>> Create(
            [FromBody] CreateCategoryRequest dto,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { Message = "Name is required." });

            if (dto.ParentCategoryId is null)
                return BadRequest(new { Message = "ParentCategoryId is required." });

            var parent = await _context.Categories.FirstOrDefaultAsync(
                x => x.Id == dto.ParentCategoryId,
                ct
            );
            if (parent is null)
                return BadRequest(new { Message = $"Parent {dto.ParentCategoryId} not found." });

            if (!parent.IsSystemCategory)
                return BadRequest(
                    new { Message = "Only system categories can have subcategories." }
                );

            var entity = dto.ToEntity(); // IsSystemCategory defaultar till false i modellen
            _context.Categories.Add(entity);
            await _context.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity.ToDetailDto());
        }

        // PUT: api/categories/5
        // Body: { "name": "...", "parentCategoryId": 1 }
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateCategoryRequest dto,
            CancellationToken ct = default
        )
        {
            var c = await _context.Categories.FindAsync(new object[] { id }, ct);
            if (c is null)
                return NotFound();

            // systemkategorier är låsta
            if (c.IsSystemCategory)
                return BadRequest(new { Message = "System categories cannot be modified." });

            if (dto.ParentCategoryId.HasValue)
            {
                if (dto.ParentCategoryId == id)
                    return BadRequest(new { Message = "Parent cannot be self." });

                var parent = await _context.Categories.FirstOrDefaultAsync(
                    x => x.Id == dto.ParentCategoryId,
                    ct
                );
                if (parent is null)
                    return BadRequest(new { Message = "Parent not found." });

                if (!parent.IsSystemCategory)
                    return BadRequest(
                        new { Message = "Only system categories can have subcategories." }
                    );

                c.ParentCategoryId = dto.ParentCategoryId;
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
                c.Name = dto.Name.Trim();

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

            var hasChildren = await _context.Categories.AnyAsync(x => x.ParentCategoryId == id, ct);
            if (hasChildren)
                return BadRequest(new { Message = "Has child categories." });

            var inUse =
                await _context.Transactions.AnyAsync(t => t.CategoryId == id, ct)
                || await _context.CategorizationRules.AnyAsync(h => h.CategoryId == id, ct);
            if (inUse)
                return BadRequest(new { Message = "Category in use." });

            _context.Categories.Remove(c);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
