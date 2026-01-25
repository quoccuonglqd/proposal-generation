using Cherry.Core.Entities;
using Cherry.Core.Interfaces;
using Cherry.Infrastructure.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cherry.Infrastructure.Services
{
    public class PptxGeneratorService : IDocumentService
    {
        private readonly ILogger<PptxGeneratorService> _logger;
        private readonly CherryDbContext _context;
        private readonly IStorageService _storageService;

        public PptxGeneratorService(CherryDbContext context, IStorageService storageService, ILogger<PptxGeneratorService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task GeneratePptxAsync(Guid proposalVersionId)
        {
            var version = await _context.ProposalVersions
                .Include(v => v.Proposal)
                    .ThenInclude(p => p.Sections)
                .Include(v => v.Proposal)
                    .ThenInclude(p => p.Region)
                .Include(v => v.Proposal)
                    .ThenInclude(p => p.ServiceSelections)
                        .ThenInclude(s => s.Service)
                .FirstOrDefaultAsync(v => v.Id == proposalVersionId);

            if (version == null) return;

            try 
            {
                // Load Template
                Cherry.Core.Entities.Template? template = null;
                if (version.Proposal.TemplateId.HasValue)
                {
                    template = await _context.Templates.FindAsync(version.Proposal.TemplateId.Value);
                }

                if (template == null)
                {
                    template = await _context.Templates
                        .OrderByDescending(t => t.Version)
                        .FirstOrDefaultAsync(t => t.Status == TemplateStatus.Active);
                }

                if (template == null) throw new Exception("No active template found");

                var templatePath = _storageService.GetStoragePath(template.StorageKey);
                var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pptx");

                File.Copy(templatePath, outputPath);

                using (PresentationDocument doc = PresentationDocument.Open(outputPath, true))
                {
                    var presentationPart = doc.PresentationPart;
                    if (presentationPart != null)
                    {
                        foreach (var slideId in presentationPart.Presentation.SlideIdList!.Elements<SlideId>())
                        {
                            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
                            ReplacePlaceholders(slidePart, version);
                        }
                    }
                    doc.Save();
                }

                // Save artifact
                using (var stream = new FileStream(outputPath, FileMode.Open))
                {
                    var storageKey = await _storageService.SaveFileAsync(stream, $"Proposal_{version.Proposal.ClientName.Replace(" ", "_")}.pptx", "proposals");
                    
                    var artifact = new ProposalArtifact
                    {
                        ProposalVersionId = version.Id,
                        Type = ArtifactType.Pptx,
                        StorageKey = storageKey
                    };
                    _context.ProposalArtifacts.Add(artifact);
                    await _context.SaveChangesAsync();

                    // Update proposal status independently to avoid concurrency tracking issues
                    await _context.Proposals
                        .Where(p => p.Id == version.ProposalId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.Status, ProposalStatus.Generated)
                            .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
                }

                File.Delete(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PPTX for proposal version {VersionId}", proposalVersionId);
                
                try 
                {
                    await _context.Proposals
                        .Where(p => p.Id == version.ProposalId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.Status, ProposalStatus.Error)
                            .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
                }
                catch (Exception statusEx)
                {
                    _logger.LogError(statusEx, "Failed to update proposal status to ERROR for version {VersionId}", proposalVersionId);
                }
            }
        }

        private void ReplacePlaceholders(SlidePart slidePart, ProposalVersion version)
        {
            var textElements = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>().ToList();
            foreach (var text in textElements)
            {
                var content = text.Text;
                
                // Basic Proposal Data
                if (content.Contains("{{proposal.clientName}}"))
                    content = content.Replace("{{proposal.clientName}}", version.Proposal.ClientName);
                
                if (content.Contains("{{proposal.date}}"))
                    content = content.Replace("{{proposal.date}}", version.Proposal.UpdatedAt.ToString("dd MMM yyyy"));

                if (content.Contains("{{proposal.region}}"))
                    content = content.Replace("{{proposal.region}}", version.Proposal.Region.Name);

                // Services List (Simplified as a multi-line string for now)
                if (content.Contains("{{services.list}}"))
                {
                    var servicesText = string.Join("\n", version.Proposal.ServiceSelections.Select(s => $"- {s.Service.Name} (Qty: {s.Quantity})"));
                    content = content.Replace("{{services.list}}", servicesText);
                }

                // Section Contents
                foreach (var section in version.Proposal.Sections)
                {
                    var placeholder = $"{{{{section.{section.SectionKey}.content}}}}";
                    if (content.Contains(placeholder))
                    {
                        try 
                        {
                            var contentObj = JsonSerializer.Deserialize<Dictionary<string, string>>(section.ContentJson);
                            
                            // 1. Try language-specific (TODO: dynamic language)
                            string? textVal = null;
                            if (contentObj != null)
                            {
                                if (contentObj.ContainsKey("en")) textVal = contentObj["en"];
                                else if (contentObj.ContainsKey("text")) textVal = contentObj["text"];
                                else textVal = contentObj.Values.FirstOrDefault(); // Fallback to first string
                            }

                            if (!string.IsNullOrEmpty(textVal))
                            {
                                content = content.Replace(placeholder, textVal);
                            }
                        }
                        catch { /* Ignore malformed JSON */ }
                    }
                }

                text.Text = content;
            }
        }

        public Task ConvertPptxToPdfAsync(Guid artifactId)
        {
            // Implementation for PDF conversion
            return Task.CompletedTask;
        }
    }
}
