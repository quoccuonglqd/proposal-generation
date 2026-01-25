using Cherry.Core.Entities;
using Cherry.Core.Interfaces;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/templates")]
    [Authorize]
    public class TemplatesController : ControllerBase
    {
        private readonly CherryDbContext _context;
        private readonly IStorageService _storageService;

        public TemplatesController(CherryDbContext context, IStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Template>>> List()
        {
            return await _context.Templates
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] int version, [FromForm] string? description, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("PPTX file is required");

            using (var stream = file.OpenReadStream())
            {
                var storageKey = await _storageService.SaveFileAsync(stream, file.FileName, "templates");
                
                var template = new Template
                {
                    Name = name,
                    Version = version,
                    Description = description,
                    StorageKey = storageKey,
                    Status = TemplateStatus.Draft
                };

                _context.Templates.Add(template);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(List), new { id = template.Id }, template);
            }
        }

        [HttpPost("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Activate(Guid id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return NotFound();

            // Deactivate others of the same name if needed, or just set this one
            template.Status = TemplateStatus.Active;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return NotFound();

            template.Status = TemplateStatus.Inactive;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("{id}/validate")]
        [Authorize(Roles = "Admin")]
        public IActionResult Validate(Guid id)
        {
            // Placeholder for OpenXML validation logic
            return Ok(new { Status = "PASS", Warnings = new List<string>() });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return NotFound();

            // Delete file if it exists
            try
            {
                var fullPath = _storageService.GetStoragePath(template.StorageKey);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with DB deletion
                Console.WriteLine($"[ERROR] Failed to delete template file: {ex.Message}");
            }

            _context.Templates.Remove(template);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
