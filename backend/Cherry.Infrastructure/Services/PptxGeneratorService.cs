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
                var outputPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid()}.pptx");

                File.Copy(templatePath, outputPath);

                using (PresentationDocument doc = PresentationDocument.Open(outputPath, true))
                {
                    var presentationPart = doc.PresentationPart;
                    if (presentationPart != null)
                    {
                        // 1. Process Master Slides
                        foreach (var masterPart in presentationPart.SlideMasterParts)
                        {
                            ReplacePlaceholdersInPart(masterPart, version);
                            
                            // 2. Process Layout Slides (often linked from masters)
                            foreach (var layoutPart in masterPart.SlideLayoutParts)
                            {
                                ReplacePlaceholdersInPart(layoutPart, version);
                            }
                        }

                        // 3. Process Individual Slides
                        foreach (var slideId in presentationPart.Presentation.SlideIdList!.Elements<SlideId>())
                        {
                            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
                            ReplacePlaceholdersInPart(slidePart, version);
                        }
                    }
                    doc.Save();
                }

                // Save artifact
                using (var stream = new System.IO.FileStream(outputPath, System.IO.FileMode.Open))
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

        private void ReplacePlaceholdersInPart(OpenXmlPart part, ProposalVersion version)
        {
            // Resolve the root element that contains drawings/paragraphs
            DocumentFormat.OpenXml.OpenXmlCompositeElement? root = null;
            if (part is SlidePart sp) root = sp.Slide;
            else if (part is SlideLayoutPart slp) root = slp.SlideLayout;
            else if (part is SlideMasterPart smp) root = smp.SlideMaster;

            if (root == null) return;

            var paragraphs = root.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>().ToList();
            var language = version.Proposal.DefaultLanguage ?? "en";

            foreach (var para in paragraphs)
            {
                var textElements = para.Descendants<DocumentFormat.OpenXml.Drawing.Text>().ToList();
                if (!textElements.Any()) continue;

                // Aggregate full text to handle placeholders split across elements
                var fullText = string.Concat(textElements.Select(t => t.Text));
                var originalText = fullText;

                // 1. Basic Proposal Data
                fullText = fullText.Replace("{{proposal.clientName}}", version.Proposal.ClientName);
                fullText = fullText.Replace("{{proposal.date}}", version.Proposal.UpdatedAt.ToString("dd MMM yyyy"));
                fullText = fullText.Replace("{{proposal.region}}", version.Proposal.Region.Name);

                // 2. Services List
                if (fullText.Contains("{{services.list}}"))
                {
                    var servicesText = string.Join("\n", version.Proposal.ServiceSelections.Select(s => $"- {s.Service.Name} (Qty: {s.Quantity})"));
                    fullText = fullText.Replace("{{services.list}}", servicesText);
                }

                // 3. Section Contents (Indexed: {{section.KEY.INDEX.FIELD}})
                var indexedMatches = System.Text.RegularExpressions.Regex.Matches(fullText, @"\{\{section\.([^.]+)\.(\d+)\.(title|content)\}\}");
                foreach (System.Text.RegularExpressions.Match match in indexedMatches)
                {
                    var sectionKey = match.Groups[1].Value;
                    var index = int.Parse(match.Groups[2].Value);
                    var field = match.Groups[3].Value;

                    _logger.LogInformation("Matched indexed placeholder: {SectionKey}, Index: {Index}, Field: {Field}", sectionKey, index, field);

                    var val = GetContentValue(version, sectionKey, language, index, field);
                    if (val != null) fullText = fullText.Replace(match.Value, val);

                    // If we found a title placeholder, also try to apply the background image
                    if (field == "title" && part is SlidePart slidePart)
                    {
                        var backgroundAssetId = GetSlideBackgroundId(version, sectionKey, language, index);
                        if (!string.IsNullOrEmpty(backgroundAssetId))
                        {
                            ApplyBackgroundToSlide(slidePart, backgroundAssetId);
                        }
                    }
                }

                // 4. Section Contents (Legacy: {{section.KEY.content}})
                var legacyMatches = System.Text.RegularExpressions.Regex.Matches(fullText, @"\{\{section\.([^.]+)\.content\}\}");
                foreach (System.Text.RegularExpressions.Match match in legacyMatches)
                {
                    var sectionKey = match.Groups[1].Value;
                    _logger.LogInformation("Matched legacy placeholder: {SectionKey}", sectionKey);
                    
                    var val = GetContentValue(version, sectionKey, language, null, "content");
                    if (val == null && sectionKey.Contains("_"))
                    {
                        // Try space normalization
                        val = GetContentValue(version, sectionKey.Replace("_", " "), language, null, "content");
                    }
                    
                    if (val != null) fullText = fullText.Replace(match.Value, val);

                    // For legacy placeholders, try applying background to index 0
                    if (part is SlidePart slidePart)
                    {
                        var backgroundAssetId = GetSlideBackgroundId(version, sectionKey, language, 0);
                        if (string.IsNullOrEmpty(backgroundAssetId) && sectionKey.Contains("_"))
                        {
                            backgroundAssetId = GetSlideBackgroundId(version, sectionKey.Replace("_", " "), language, 0);
                        }

                        if (!string.IsNullOrEmpty(backgroundAssetId))
                        {
                            _logger.LogInformation("Applying background for legacy section {SectionKey} from asset {AssetId}", sectionKey, backgroundAssetId);
                            ApplyBackgroundToSlide(slidePart, backgroundAssetId);
                        }
                    }
                }

                // If text changed, update OpenXml elements
                if (fullText != originalText)
                {
                    var paragraph = textElements[0].Ancestors<DocumentFormat.OpenXml.Drawing.Paragraph>().FirstOrDefault();
                    if (paragraph != null)
                    {
                        // Remove all existing runs and breaks
                        var runsToRemove = paragraph.Elements<DocumentFormat.OpenXml.Drawing.Run>().ToList();
                        var breaksToRemove = paragraph.Elements<DocumentFormat.OpenXml.Drawing.Break>().ToList();
                        
                        // Capture formatting from the first run if it exists
                        var firstRunProps = runsToRemove.FirstOrDefault()?.RunProperties;

                        foreach (var r in runsToRemove) r.Remove();
                        foreach (var b in breaksToRemove) b.Remove();

                        // Split by newline and reconstruct
                        var lines = fullText.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var run = new DocumentFormat.OpenXml.Drawing.Run();
                            if (firstRunProps != null)
                            {
                                run.RunProperties = (DocumentFormat.OpenXml.Drawing.RunProperties)firstRunProps.CloneNode(true);
                            }
                            run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text(lines[i]));
                            paragraph.AppendChild(run);

                            if (i < lines.Length - 1)
                            {
                                paragraph.AppendChild(new DocumentFormat.OpenXml.Drawing.Break());
                            }
                        }
                    }
                }
            }
        }

        private string? GetContentValue(ProposalVersion version, string sectionKey, string language, int? index, string field)
        {
            var section = version.Proposal.Sections.FirstOrDefault(s => s.SectionKey == sectionKey);
            if (section == null && sectionKey.Contains("_"))
            {
                // Try space normalization if the key has underscores but the DB might have spaces
                var normalizedKey = sectionKey.Replace("_", " ");
                section = version.Proposal.Sections.FirstOrDefault(s => s.SectionKey == normalizedKey);
            }

            if (section == null) return null;

            try 
            {
                using var doc = JsonDocument.Parse(section.ContentJson);
                
                // Try target language, fallback to English
                if (!doc.RootElement.TryGetProperty(language, out var langRoot))
                {
                    if (!doc.RootElement.TryGetProperty("en", out langRoot)) return null;
                }

                if (index.HasValue)
                {
                    // Case: {{section.KEY.INDEX.FIELD}} - Expecting Array of Objects
                    if (langRoot.ValueKind == JsonValueKind.Array && index.Value < langRoot.GetArrayLength())
                    {
                        var item = langRoot[index.Value];
                        if (item.TryGetProperty(field, out var prop)) return prop.GetString();
                    }
                }
                else
                {
                    // Case: {{section.KEY.content}} - Handle both String and Array of Objects
                    if (langRoot.ValueKind == JsonValueKind.String) return langRoot.GetString();
                    if (langRoot.ValueKind == JsonValueKind.Array && langRoot.GetArrayLength() > 0)
                    {
                        var first = langRoot[0];
                        if (first.TryGetProperty("content", out var prop)) return prop.GetString();
                    }
                }
            }
            catch { /* Ignore malformed JSON */ }

            return null;
        }

        private string? GetSlideBackgroundId(ProposalVersion version, string sectionKey, string language, int index)
        {
            var section = version.Proposal.Sections.FirstOrDefault(s => s.SectionKey == sectionKey);
            if (section == null && sectionKey.Contains("_"))
            {
                var normalizedKey = sectionKey.Replace("_", " ");
                section = version.Proposal.Sections.FirstOrDefault(s => s.SectionKey == normalizedKey);
            }

            if (section == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(section.ContentJson);
                if (!doc.RootElement.TryGetProperty(language, out var langRoot))
                {
                    if (!doc.RootElement.TryGetProperty("en", out langRoot)) return null;
                }

                if (langRoot.ValueKind == JsonValueKind.Array && index < langRoot.GetArrayLength())
                {
                    var item = langRoot[index];
                    if (item.TryGetProperty("backgroundAssetId", out var prop)) return prop.GetString();
                }
            }
            catch { }
            return null;
        }

        private void ApplyBackgroundToSlide(SlidePart slidePart, string backgroundAssetId)
        {
            try
            {
                var storagePath = _storageService.GetStoragePath(backgroundAssetId);
                if (!File.Exists(storagePath))
                {
                    _logger.LogWarning("Background asset physical file not found at {Path}", storagePath);
                    return;
                }

                // 1. Ensure Background element exists and is the FIRST child
                if (slidePart.Slide.CommonSlideData == null) slidePart.Slide.CommonSlideData = new CommonSlideData();
                
                var cSld = slidePart.Slide.CommonSlideData;
                var bg = cSld.Background;
                if (bg == null)
                {
                    bg = new Background();
                    cSld.InsertAt(bg, 0);
                }

                // Clear existing background properties AND references to force override
                bg.RemoveAllChildren<BackgroundProperties>();
                bg.RemoveAllChildren<BackgroundStyleReference>();
                
                var bgPr = new BackgroundProperties();
                var blipFill = new DocumentFormat.OpenXml.Drawing.BlipFill();
                var blip = new DocumentFormat.OpenXml.Drawing.Blip();
                var stretch = new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle());

                // 2. Add Image Part
                var imagePart = slidePart.AddImagePart(ImagePartType.Png);
                using (var stream = System.IO.File.OpenRead(storagePath))
                {
                    imagePart.FeedData(stream);
                }

                blip.Embed = slidePart.GetIdOfPart(imagePart);
                blipFill.Append(blip);
                blipFill.Append(stretch);
                bgPr.Append(blipFill);
                bg.Append(bgPr);
                
                _logger.LogInformation("Successfully applied background {AssetId} to slide via ImagePart {PartId}", backgroundAssetId, slidePart.GetIdOfPart(imagePart));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply background image {AssetId} to slide", backgroundAssetId);
            }
        }

        public Task ConvertPptxToPdfAsync(Guid artifactId)
        {
            // Implementation for PDF conversion
            return Task.CompletedTask;
        }
    }
}
