using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admin/sections")]
    [Authorize(Roles = "Admin")]
    public class MasterSectionsController : ControllerBase
    {
        private readonly CherryDbContext _context;

        public MasterSectionsController(CherryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MasterSectionDto>>> GetMasterSections()
        {
            var sections = await _context.MasterSections
                .OrderBy(s => s.SortOrder)
                .Select(s => new MasterSectionDto(
                    s.Id,
                    s.SectionKey,
                    s.Name,
                    JsonSerializer.Deserialize<object>(s.DefaultContentJson, (JsonSerializerOptions?)null) ?? new object(),
                    s.SortOrder,
                    s.IsActive
                ))
                .ToListAsync();

            return Ok(sections);
        }

        [HttpPost]
        public async Task<ActionResult<MasterSectionDto>> CreateMasterSection([FromBody] UpsertMasterSectionRequest request)
        {
            var section = new MasterSection
            {
                SectionKey = request.SectionKey,
                Name = request.Name,
                DefaultContentJson = JsonSerializer.Serialize(request.DefaultContent),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive
            };

            _context.MasterSections.Add(section);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMasterSections), new { id = section.Id }, new MasterSectionDto(
                section.Id,
                section.SectionKey,
                section.Name,
                request.DefaultContent,
                section.SortOrder,
                section.IsActive
            ));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMasterSection(Guid id, [FromBody] UpsertMasterSectionRequest request)
        {
            var section = await _context.MasterSections.FindAsync(id);
            if (section == null) return NotFound();

            section.SectionKey = request.SectionKey;
            section.Name = request.Name;
            section.DefaultContentJson = JsonSerializer.Serialize(request.DefaultContent);
            section.SortOrder = request.SortOrder;
            section.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMasterSection(Guid id)
        {
            var section = await _context.MasterSections.FindAsync(id);
            if (section == null) return NotFound();

            _context.MasterSections.Remove(section);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
