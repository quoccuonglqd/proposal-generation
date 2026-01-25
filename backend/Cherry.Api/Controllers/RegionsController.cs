using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/regions")]
    [Authorize]
    public class RegionsController : ControllerBase
    {
        private readonly CherryDbContext _context;

        public RegionsController(CherryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegionDto>>> GetRegions()
        {
            return await _context.Regions
                .Where(r => r.IsActive)
                .Select(r => new RegionDto(r.Id, r.Code, r.Name, r.LocalCurrency))
                .ToListAsync();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Region>> Create([FromBody] Region region)
        {
            _context.Regions.Add(region);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetRegions), new { id = region.Id }, region);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Region region)
        {
            var existing = await _context.Regions.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = region.Name;
            existing.Code = region.Code;
            existing.LocalCurrency = region.LocalCurrency;
            existing.IsActive = region.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var region = await _context.Regions.FindAsync(id);
            if (region == null) return NotFound();

            region.IsActive = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
