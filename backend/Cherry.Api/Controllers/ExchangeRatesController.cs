using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/exchange-rates")]
    [Authorize]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly CherryDbContext _context;

        public ExchangeRatesController(CherryDbContext context)
        {
            _context = context;
        }

        [HttpGet("default")]
        public async Task<ActionResult<ExchangeRateRule>> GetDefaultRate([FromQuery] Guid regionId)
        {
            var region = await _context.Regions.FindAsync(regionId);
            if (region == null) return NotFound("Region not found");

            var rate = await _context.ExchangeRateRules
                .Where(r => r.QuoteCurrency == region.LocalCurrency && r.IsDefault)
                .OrderByDescending(r => r.ValidFrom)
                .FirstOrDefaultAsync();

            if (rate == null)
            {
                // Fallback or default if none found
                return Ok(new
                {
                    Base = "USD",
                    Quote = region.LocalCurrency,
                    Rate = 1.0m,
                    Source = "No default set"
                });
            }

            return Ok(rate);
        }

        [HttpPut("default")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpsertDefaultRate([FromBody] ExchangeRateRule request)
        {
            var existing = await _context.ExchangeRateRules
                .Where(r => r.QuoteCurrency == request.QuoteCurrency && r.IsDefault)
                .ToListAsync();

            foreach (var r in existing) r.IsDefault = false;

            request.Id = Guid.NewGuid();
            request.IsDefault = true;
            _context.ExchangeRateRules.Add(request);

            await _context.SaveChangesAsync();
            return Ok(request);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ExchangeRateRule>>> List()
        {
            return await _context.ExchangeRateRules
                .OrderByDescending(r => r.ValidFrom)
                .ToListAsync();
        }
    }
}
