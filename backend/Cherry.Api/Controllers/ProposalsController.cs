using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Cherry.Core.Interfaces;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Hangfire;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/proposals")]
    [Authorize]
    public class ProposalsController : ControllerBase
    {
        private readonly CherryDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IStorageService _storageService;

        public ProposalsController(CherryDbContext context, IBackgroundJobClient backgroundJobClient, IStorageService storageService)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
            _storageService = storageService;
        }




        [HttpGet]
        public async Task<ActionResult<object>> List([FromQuery] string? status, [FromQuery] Guid? regionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Proposals
                .Include(p => p.Region)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ProposalStatus>(status, true, out var statusEnum))
            {
                query = query.Where(p => p.Status == statusEnum);
            }

            if (regionId.HasValue)
            {
                query = query.Where(p => p.RegionId == regionId.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProposalSummaryDto(p.Id, p.ClientName, p.ProjectName, p.Region.Name, p.Status.ToString().ToUpper(), p.UpdatedAt))
                .ToListAsync();

            return Ok(new { Items = items, Total = total, Page = page, PageSize = pageSize });
        }

        [HttpGet("{proposalId:guid}")]
        public async Task<ActionResult<ProposalDetailDto>> GetById(Guid proposalId)
        {
            var p = await _context.Proposals
                .Include(p => p.Region)
                .Include(p => p.Sections)
                .Include(p => p.ServiceSelections)
                    .ThenInclude(s => s.Service)
                .Include(p => p.Versions)
                    .ThenInclude(v => v.Artifacts)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (p == null) return NotFound();

            var sections = p.Sections
                .OrderBy(s => s.Order)
                .Select(s => new ProposalSectionDto(
                    s.SectionKey, 
                    s.Order, 
                    s.BackgroundType, 
                    s.BackgroundAssetId, 
                    JsonSerializer.Deserialize<object>(s.ContentJson) ?? new object()))
                .ToList();

            Console.WriteLine($"[DEBUG] Loading Proposal {p.Id} for Region {p.RegionId} ({p.Region.Code})");

            var services = p.ServiceSelections
                .Select(s => {
                    var price = _context.ServicePrices
                        .AsNoTracking()
                        .FirstOrDefault(sp => sp.ServiceId == s.ServiceId && sp.RegionId == p.RegionId && sp.Status == "Active");
                    
                    if (price == null) {
                        Console.WriteLine($"[WARNING] No active price found for Service {s.ServiceId} in Region {p.RegionId}");
                    }

                    return new ProposalServiceSelectionDto(
                        s.ServiceId, 
                        s.Service.Name, 
                        s.Quantity, 
                        s.Notes, 
                        new PriceDto(price?.LocalPrice ?? 0, price?.UsdReferencePrice ?? 0));
                })
                .ToList();

            var artifacts = new List<ProposalArtifactDto>();
            var latestVersion = p.Versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
            if (latestVersion != null)
            {
                foreach (var a in latestVersion.Artifacts)
                {
                    artifacts.Add(new ProposalArtifactDto(
                        a.Id,
                        a.Type.ToString().ToUpper(),
                        await _storageService.GetPresignedUrlAsync(a.StorageKey, TimeSpan.FromHours(1)),
                        Path.GetFileName(a.StorageKey)));
                }
            }

            return Ok(new ProposalDetailDto(p.Id, p.ClientName, p.ProjectName, p.RegionId, p.Status.ToString().ToUpper(), p.DefaultLanguage, p.TemplateId, sections, services, artifacts));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<ActionResult<ProposalDetailDto>> Create([FromBody] CreateProposalRequest request)
        {
            var p = await _context.Proposals
                .Include(p => p.Sections)
                .FirstOrDefaultAsync(x => x.ClientName == request.ClientName && x.Status == ProposalStatus.Draft);
            
            if (p != null)
            {
                // Update existing draft if same client
                p.ProjectName = request.ProjectName;
                p.RegionId = request.RegionId;
                p.TemplateId = request.TemplateId;
                p.DefaultLanguage = request.LanguageDefault ?? "en";
                p.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                p = new Proposal
                {
                    ClientName = request.ClientName,
                    ProjectName = request.ProjectName,
                    RegionId = request.RegionId,
                    TemplateId = request.TemplateId,
                    CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system",
                    DefaultLanguage = request.LanguageDefault ?? "en",
                    Status = ProposalStatus.Draft
                };
                _context.Proposals.Add(p);
            }

            // Populate sections from MasterSections defined in Admin Console
            if (!p.Sections.Any())
            {
                var masterSections = await _context.MasterSections
                    .Where(ms => ms.IsActive)
                    .OrderBy(ms => ms.SortOrder)
                    .ToListAsync();
                
                foreach (var ms in masterSections)
                {
                    p.Sections.Add(new ProposalSection 
                    { 
                        SectionKey = ms.SectionKey, 
                        Order = ms.SortOrder, 
                        ContentJson = ms.DefaultContentJson 
                    });
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"[DEBUG] Saved Proposal {p.Id} with {p.Sections.Count} sections.");

            // Return detail DTO
            var sections = p.Sections
                .OrderBy(s => s.Order)
                .Select(s => new ProposalSectionDto(
                    s.SectionKey, 
                    s.Order, 
                    s.BackgroundType, 
                    s.BackgroundAssetId, 
                    JsonSerializer.Deserialize<object>(s.ContentJson) ?? new object()))
                .ToList();

            return Ok(new ProposalDetailDto(p.Id, p.ClientName, p.ProjectName, p.RegionId, p.Status.ToString().ToUpper(), p.DefaultLanguage, p.TemplateId, sections, new List<ProposalServiceSelectionDto>(), new List<ProposalArtifactDto>()));
        }

        [HttpPatch("{proposalId:guid}/metadata")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> UpdateMetadata(Guid proposalId, [FromBody] UpdateProposalMetadataRequest request)
        {
            var p = await _context.Proposals.FindAsync(proposalId);
            if (p == null) return NotFound();

            p.ClientName = request.ClientName;
            p.ProjectName = request.ProjectName;
            p.RegionId = request.RegionId;
            p.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{proposalId:guid}")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> Delete(Guid proposalId)
        {
            var p = await _context.Proposals.FindAsync(proposalId);
            if (p == null) return NotFound();

            _context.Proposals.Remove(p);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("batch-delete")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest request)
        {
            if (request.ProposalIds == null || !request.ProposalIds.Any()) 
                return BadRequest("No proposal IDs provided.");

            var ids = request.ProposalIds;
            Console.WriteLine($"[DEBUG] Batch deleting proposals: {string.Join(", ", ids)}");

            try 
            {
                // 1. Get all Version IDs first (to avoid joins in ExecuteDeleteAsync)
                var versionIds = await _context.ProposalVersions
                    .Where(v => ids.Contains(v.ProposalId))
                    .Select(v => v.Id)
                    .ToListAsync();

                // 2. Delete Artifacts (join-free)
                if (versionIds.Any())
                {
                    await _context.ProposalArtifacts
                        .Where(a => versionIds.Contains(a.ProposalVersionId))
                        .ExecuteDeleteAsync();
                }

                // 3. Delete Versions (join-free)
                await _context.ProposalVersions
                    .Where(v => ids.Contains(v.ProposalId))
                    .ExecuteDeleteAsync();

                // 4. Delete Sections
                await _context.ProposalSections
                    .Where(s => ids.Contains(s.ProposalId))
                    .ExecuteDeleteAsync();

                // 5. Delete Selections
                await _context.ProposalServiceSelections
                    .Where(s => ids.Contains(s.ProposalId))
                    .ExecuteDeleteAsync();

                // 6. Finally delete the proposals
                await _context.Proposals
                    .Where(p => ids.Contains(p.Id))
                    .ExecuteDeleteAsync();

                Console.WriteLine("[DEBUG] Batch delete successful.");
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Batch delete failed: {ex.Message}");
                return StatusCode(500, new { message = "Database error during batch delete", detail = ex.Message });
            }
        }

        [HttpPut("{proposalId:guid}/services")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> UpdateServices(Guid proposalId, [FromBody] UpdateProposalServicesRequest request)
        {
            Console.WriteLine($"[DEBUG] Updating services for Proposal {proposalId}. Item count: {request.Items?.Count ?? 0}");

            // 1. Nuke existing selections (bypass tracker for deletion)
            await _context.ProposalServiceSelections
                .Where(s => s.ProposalId == proposalId)
                .ExecuteDeleteAsync();

            // 2. Load the proposal and update its timestamp
            var p = await _context.Proposals.FindAsync(proposalId);
            if (p == null) return NotFound();

            p.UpdatedAt = DateTime.UtcNow;

            // 3. Add new selections
            if (request.Items != null && request.Items.Any())
            {
                foreach (var item in request.Items)
                {
                    Console.WriteLine($"[DEBUG] Adding Service {item.ServiceId} with Quantity {item.Quantity}");
                    _context.ProposalServiceSelections.Add(new ProposalServiceSelection
                    {
                        ProposalId = proposalId,
                        ServiceId = item.ServiceId,
                        Quantity = item.Quantity,
                        Notes = item.Notes
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{proposalId:guid}/sections/order")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> UpdateSectionsOrder(Guid proposalId, [FromBody] UpdateSectionsOrderRequest request)
        {
            var exists = await _context.Proposals.AnyAsync(p => p.Id == proposalId);
            if (!exists) return NotFound();

            var sections = await _context.ProposalSections
                .Where(s => s.ProposalId == proposalId)
                .ToListAsync();

            for (int i = 0; i < request.OrderedKeys.Count; i++)
            {
                var key = request.OrderedKeys[i];
                var section = sections.FirstOrDefault(s => s.SectionKey == key);
                if (section != null)
                {
                    section.Order = i + 1;
                }
                else
                {
                    _context.ProposalSections.Add(new ProposalSection
                    {
                        ProposalId = proposalId,
                        SectionKey = key,
                        Order = i + 1,
                        ContentJson = "{}"
                    });
                }
            }

            // Refresh UpdatedAt timestamp directly
            await _context.Proposals
                .Where(p => p.Id == proposalId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{proposalId:guid}/sections/{sectionKey}")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> UpdateSectionContent(Guid proposalId, string sectionKey, [FromBody] UpdateSectionContentRequest request)
        {
            var section = await _context.ProposalSections
                .FirstOrDefaultAsync(s => s.ProposalId == proposalId && s.SectionKey == sectionKey);

            if (section == null)
            {
                section = new ProposalSection
                {
                    ProposalId = proposalId,
                    SectionKey = sectionKey,
                    Order = (_context.ProposalSections.Where(s => s.ProposalId == proposalId).Max(s => (int?)s.Order) ?? 0) + 1
                };
                _context.ProposalSections.Add(section);
            }

            section.BackgroundType = request.Background.Type;
            section.BackgroundAssetId = request.Background.AssetId;
            section.ContentJson = JsonSerializer.Serialize(request.Content);

            // Refresh UpdatedAt timestamp directly
            await _context.Proposals
                .Where(p => p.Id == proposalId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("debug/db-status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDbStatus()
        {
            var prices = await _context.ServicePrices.Select(p => new { p.ServiceId, p.RegionId, p.LocalPrice, p.Status }).ToListAsync();
            var selections = await _context.ProposalServiceSelections.Select(s => new { s.ProposalId, s.ServiceId, s.Quantity }).ToListAsync();
            var proposals = await _context.Proposals.Select(p => new { p.Id, p.ClientName, p.RegionId, p.Status }).ToListAsync();
            var regions = await _context.Regions.Select(r => new { r.Id, r.Code, r.Name }).ToListAsync();
            
            return Ok(new { 
                Prices = prices, 
                Selections = selections, 
                Proposals = proposals,
                Regions = regions
            });
        }

        [HttpPost("{proposalId:guid}/generate")]
        [Authorize(Roles = "Admin,ProposalCreator")]
        public async Task<IActionResult> Generate(Guid proposalId, [FromBody] GenerateProposalRequest request)
        {
            var p = await _context.Proposals.FindAsync(proposalId);
            if (p == null) return NotFound();

            var version = new ProposalVersion
            {
                ProposalId = proposalId,
                VersionNo = (_context.ProposalVersions.Where(v => v.ProposalId == proposalId).Max(s => (int?)s.VersionNo) ?? 0) + 1,
                ExchangeRateUsed = request.ExchangeRateOverride?.Rate ?? 1.0m, // Logic for default rate can be added
                CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system"
            };

            _context.ProposalVersions.Add(version);
            
            // Use direct update for status and timestamp to avoid concurrency issues
            await _context.Proposals
                .Where(p => p.Id == proposalId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, ProposalStatus.Generating)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

            await _context.SaveChangesAsync();

            var jobId = _backgroundJobClient.Enqueue<Cherry.Core.Interfaces.IDocumentService>(x => x.GeneratePptxAsync(version.Id));

            return Accepted(new { VersionId = version.Id, JobId = jobId });
        }
    }
}
