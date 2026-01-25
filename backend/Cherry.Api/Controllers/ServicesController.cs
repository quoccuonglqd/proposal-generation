using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/services")]
    [Authorize]
    public class ServicesController : ControllerBase
    {
        private readonly CherryDbContext _context;

        public ServicesController(CherryDbContext context)
        {
            _context = context;
        }

        [HttpGet("tree")]
        public async Task<ActionResult<object>> GetServiceTree([FromQuery] Guid regionId)
        {
            var region = await _context.Regions.FindAsync(regionId);
            if (region == null) return NotFound("Region not found");

            var allServices = await _context.Services
                .Include(s => s.Prices.Where(p => p.RegionId == regionId && p.Status == "Active"))
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            var tree = BuildTree(allServices, null);

            return Ok(new
            {
                RegionId = regionId,
                Currency = region.LocalCurrency,
                Items = tree
            });
        }

        private List<ServiceTreeDto> BuildTree(List<Service> allServices, Guid? parentId)
        {
            return allServices
                .Where(s => s.ParentId == parentId)
                .Select(s => new ServiceTreeDto(
                    s.Id,
                    s.Name,
                    s.Level.ToString().ToUpper(),
                    s.Prices.FirstOrDefault() is var p && p != null 
                        ? new PriceDto(p.LocalPrice, p.UsdReferencePrice) 
                        : new PriceDto(0, 0),
                    BuildTree(allServices, s.Id)
                ))
                .ToList();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateService([FromBody] UpsertServiceRequest request)
        {
            if (!Enum.TryParse<ServiceLevel>(request.Level, true, out var level))
            {
                return BadRequest("Invalid service level");
            }

            var service = new Service
            {
                ParentId = request.ParentId,
                Level = level,
                Name = request.Name,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetServiceTree), new { id = service.Id }, service);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateService(Guid id, [FromBody] UpsertServiceRequest request)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            if (!Enum.TryParse<ServiceLevel>(request.Level, true, out var level))
            {
                return BadRequest("Invalid service level");
            }

            service.ParentId = request.ParentId;
            service.Level = level;
            service.Name = request.Name;
            service.SortOrder = request.SortOrder;
            service.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("prices")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpsertPrice([FromBody] UpsertPriceRequest request)
        {
            var existingPrice = await _context.ServicePrices
                .FirstOrDefaultAsync(p => p.ServiceId == request.ServiceId && p.RegionId == request.RegionId && p.Status == "Active");

            if (existingPrice != null)
            {
                existingPrice.Status = "Inactive";
                existingPrice.EffectiveTo = DateTime.UtcNow;
            }

            var newPrice = new ServicePrice
            {
                ServiceId = request.ServiceId,
                RegionId = request.RegionId,
                LocalPrice = request.LocalPrice,
                UsdReferencePrice = request.UsdReferencePrice,
                EffectiveFrom = request.EffectiveFrom,
                Status = "Active"
            };

            _context.ServicePrices.Add(newPrice);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
