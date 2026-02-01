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
                    s.ParentId,
                    s.Name,
                    s.Level.ToString().ToUpper(),
                    s.Unit,
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

            // Hierarchy Validation
            if (request.ParentId.HasValue)
            {
                var parent = await _context.Services.FindAsync(request.ParentId.Value);
                if (parent == null) return BadRequest("Parent not found");

                if (parent.Level == ServiceLevel.Main && level != ServiceLevel.Sub)
                    return BadRequest("Main services can only contain Sub services.");
                
                if (parent.Level == ServiceLevel.Sub && (level != ServiceLevel.Range && level != ServiceLevel.LineItem))
                    return BadRequest("Sub services can only contain Range or Line Item services.");

                if (parent.Level == ServiceLevel.Range || parent.Level == ServiceLevel.LineItem)
                    return BadRequest("Range or Line Item services cannot have children.");
            }
            else if (level != ServiceLevel.Main)
            {
                return BadRequest("Root services must be of type Main.");
            }

            var service = new Service
            {
                ParentId = request.ParentId,
                Level = level,
                Name = request.Name,
                Unit = request.Unit,
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

            // Hierarchy Validation
            if (request.ParentId.HasValue)
            {
                var parent = await _context.Services.FindAsync(request.ParentId.Value);
                if (parent == null) return BadRequest("Parent not found");

                if (parent.Level == ServiceLevel.Main && level != ServiceLevel.Sub)
                    return BadRequest("Main services can only contain Sub services.");
                
                if (parent.Level == ServiceLevel.Sub && (level != ServiceLevel.Range && level != ServiceLevel.LineItem))
                    return BadRequest("Sub services can only contain Range or Line Item services.");

                if (parent.Level == ServiceLevel.Range || parent.Level == ServiceLevel.LineItem)
                    return BadRequest("Range or Line Item services cannot have children.");
            }
            else if (level != ServiceLevel.Main)
            {
                return BadRequest("Root services must be of type Main.");
            }

            service.ParentId = request.ParentId;
            service.Level = level;
            service.Name = request.Name;
            service.Unit = request.Unit;
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

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteService(Guid id)
        {
            var service = await _context.Services
                .Include(s => s.Children)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null) return NotFound();

            // Recursively delete children if any
            await DeleteServiceHierarchy(service);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task DeleteServiceHierarchy(Service service)
        {
            // Load children to ensure they are tracked and can be deleted
            await _context.Entry(service).Collection(s => s.Children).LoadAsync();
            
            foreach (var child in service.Children.ToList())
            {
                await DeleteServiceHierarchy(child);
            }

            // Delete associated prices
            var prices = await _context.ServicePrices.Where(p => p.ServiceId == service.Id).ToListAsync();
            _context.ServicePrices.RemoveRange(prices);

            _context.Services.Remove(service);
        }
    }
}
