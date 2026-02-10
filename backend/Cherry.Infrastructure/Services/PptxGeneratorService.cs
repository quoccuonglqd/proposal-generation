using Cherry.Core.Entities;
using Cherry.Core.Interfaces;
using Cherry.Infrastructure.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using System.Security;
using System.IO;
using System.Text.RegularExpressions;

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
                            .ThenInclude(s => s.Prices)
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

                        // 4. Generate Dynamic Service Table Slides at the end
                        GenerateServiceTableSlides(doc, version);
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

                        // Reconstruct paragraph
                        var lines = fullText.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            
                            // Check if this line is a Tiptap JSON block (likely from a single placeholder replacement)
                            if (line.TrimStart().StartsWith("{\"type\":\"doc\""))
                            {
                                try 
                                {
                                    _logger.LogInformation("Found Tiptap JSON: {JsonSnippet}", line.Length > 100 ? line.Substring(0, 100) + "..." : line);
                                    var parentShape = paragraph.Ancestors<DocumentFormat.OpenXml.Presentation.Shape>().FirstOrDefault();
                                    RenderTiptapToParagraph(paragraph, line.Trim(), firstRunProps, part as SlidePart, parentShape);
                                    continue; // Move to next line (if any, though usually Tiptap is the whole content)
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to render Tiptap JSON, falling back to raw text");
                                }
                            }

                            var run = new DocumentFormat.OpenXml.Drawing.Run();
                            if (firstRunProps != null)
                            {
                                run.RunProperties = (DocumentFormat.OpenXml.Drawing.RunProperties)firstRunProps.CloneNode(true);
                            }
                            run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text(line));
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
                        if (item.TryGetProperty(field, out var prop)) 
                        {
                            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
                        }
                    }
                }
                else
                {
                    // Case: {{section.KEY.content}} - Handle both String and Array of Objects
                    if (langRoot.ValueKind == JsonValueKind.String) return langRoot.GetString();
                    if (langRoot.ValueKind == JsonValueKind.Array && langRoot.GetArrayLength() > 0)
                    {
                        var first = langRoot[0];
                        if (first.TryGetProperty("content", out var prop)) 
                        {
                            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
                        }
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

        private bool ContainsPlaceholder(SlidePart part, string placeholder)
        {
            return part.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Any(t => t.Text.Contains(placeholder));
        }

        private void GenerateServiceTableSlides(PresentationDocument doc, ProposalVersion version)
        {
            var presentationPart = doc.PresentationPart!;
            
            // 1. Organize Data & Expand Hierarchy
            var selectedServiceIds = version.Proposal.ServiceSelections.Select(s => s.ServiceId).ToList();
            var allServicesInHierarchy = _context.Services
                .Include(s => s.Prices)
                .AsNoTracking()
                .ToList(); // Load all for tree building - usually small dataset

            var relevantServiceIds = new HashSet<Guid>(selectedServiceIds);
            foreach (var id in selectedServiceIds)
            {
                var current = allServicesInHierarchy.FirstOrDefault(s => s.Id == id);
                while (current?.ParentId != null)
                {
                    relevantServiceIds.Add(current.ParentId.Value);
                    current = allServicesInHierarchy.FirstOrDefault(s => s.Id == current.ParentId.Value);
                }
            }

            var tree = BuildServiceTree(version.Proposal.ServiceSelections.ToList(), allServicesInHierarchy.Where(s => relevantServiceIds.Contains(s.Id)).ToList());
            _logger.LogInformation("Built expanded service tree with {Count} root nodes", tree.Count);
            if (!tree.Any()) return;

            // 2. Identify Blueprint Slide (Last Slide)
            var lastSlideId = presentationPart.Presentation.SlideIdList!.Elements<SlideId>().LastOrDefault();
            if (lastSlideId == null) return;

            var blueprintSlidePart = (SlidePart)presentationPart.GetPartById(lastSlideId.RelationshipId!);
            var layoutPart = blueprintSlidePart.SlideLayoutPart!;

            // Extract Blueprints as OpenXmlElements
            OpenXmlElement? mainBlueprint = ExtractShapeWithPlaceholder(blueprintSlidePart, "{{service.main}}");
            OpenXmlElement? subBlueprint = ExtractShapeWithPlaceholder(blueprintSlidePart, "{{service.sub}}") 
                             ?? ExtractShapeWithPlaceholder(blueprintSlidePart, "{service.sub}");

            if (mainBlueprint == null || subBlueprint == null)
            {
                _logger.LogWarning("Could not find {{service.main}} or {{service.sub}} blueprints in the last slide.");
                return;
            }

            // 3. Pagination & Generation Loop
            long currentY = 2300000; // Starting Y after header
            const long maxY = 6200000; // Max Y before footer/bounds
            long mainHeight = GetElementHeight(mainBlueprint) ?? 565000;
            long subHeight = GetElementHeight(subBlueprint) ?? 420000;
            const long leafHeight = 420000;

            var allRows = FlattenTree(tree);
            var slideRows = new List<List<ServiceRowData>>();
            var currentSlide = new List<ServiceRowData>();

            foreach (var row in allRows)
            {
                long h = row.Level == ServiceLevel.Main ? mainHeight : (row.Level == ServiceLevel.Sub ? subHeight : leafHeight);
                if (currentY + h > maxY)
                {
                    slideRows.Add(currentSlide);
                    currentSlide = new List<ServiceRowData>();
                    currentY = 2300000;
                }
                currentSlide.Add(row);
                currentY += h;
            }
            if (currentSlide.Any()) slideRows.Add(currentSlide);

            _logger.LogInformation("Distributed services across {SlideCount} additional slides", slideRows.Count);

            ServiceRowData? lastMainInPreviousSlide = null;
            ServiceRowData? lastSubInPreviousSlide = null;

            // 4. Create Slides
            foreach (var rows in slideRows)
            {
                // CLONE the blueprint slide
                var newSlidePart = CloneSlidePart(presentationPart, blueprintSlidePart);
                
                // CLEAN placeholders from the cloned slide
                CleanPlaceholders(newSlidePart.Slide);

                var spTree = newSlidePart.Slide.CommonSlideData!.ShapeTree!;
                
                // 3. Add Rows
                long y = 2400000;
                
                // Continuation Logic
                var firstNode = rows.First();
                if (firstNode.Parent != null && firstNode.Parent != lastMainInPreviousSlide && firstNode.Parent != lastSubInPreviousSlide)
                {
                    var parentNode = firstNode.Parent;
                    var contNode = new ServiceRowData { 
                        Selection = parentNode.Selection, 
                        DisplayName = parentNode.DisplayName + " (cont.)",
                        LevelOverride = parentNode.Level
                    };
                    
                    string placeholder = contNode.Level == ServiceLevel.Main ? "{{service.main}}" : (contNode.DisplayName.Contains("{{service.sub}}") ? "{{service.sub}}" : "{service.sub}");
                    var blueprint = contNode.Level == ServiceLevel.Main ? mainBlueprint : subBlueprint;
                    
                    AppendBlueprintRow(spTree, blueprint, placeholder, contNode.DisplayName, y);
                    y += (contNode.Level == ServiceLevel.Main ? mainHeight : subHeight);
                }

                foreach (var row in rows)
                {
                    if (row.Level == ServiceLevel.Main || row.Level == ServiceLevel.Sub)
                    {
                        string placeholder = row.Level == ServiceLevel.Main ? "{{service.main}}" : (subBlueprint.InnerText.Contains("{{service.sub}}") ? "{{service.sub}}" : "{service.sub}");
                        var blueprint = row.Level == ServiceLevel.Main ? mainBlueprint : subBlueprint;
                        
                        AppendBlueprintRow(spTree, blueprint, placeholder, row.DisplayName, y);
                        y += (row.Level == ServiceLevel.Main ? mainHeight : subHeight);
                    }
                    else
                    {
                        // Leaf Level - Text Box
                        var rowXml = CreateRowXml(row, y, version.Proposal.Region.LocalCurrency, version.Proposal.RegionId);
                        spTree.Append(new DocumentFormat.OpenXml.Presentation.GroupShape(rowXml));
                        y += leafHeight;
                    }
                }
                
                lastMainInPreviousSlide = rows.LastOrDefault(r => r.Level == ServiceLevel.Main);
                lastSubInPreviousSlide = rows.LastOrDefault(r => r.Level == ServiceLevel.Sub);

                // Add Slide to Presentation at the end
                var slideIdList = presentationPart.Presentation.SlideIdList ?? presentationPart.Presentation.AppendChild(new SlideIdList());
                var slideId = new SlideId { RelationshipId = presentationPart.GetIdOfPart(newSlidePart), Id = GetNextSlideId(presentationPart) };
                slideIdList.AppendChild(slideId);
                
                _logger.LogInformation("Added new service slide with ID {SlideId} and RelId {RelId}", slideId.Id, slideId.RelationshipId);
            }

            // 5. Cleanup - Remove the blueprint slide from presentation
            presentationPart.Presentation.SlideIdList!.RemoveChild(lastSlideId);
        }

        private SlidePart CloneSlidePart(PresentationPart presentationPart, SlidePart blueprintSlidePart)
        {
            var newSlidePart = presentationPart.AddNewPart<SlidePart>();
            
            // Copy slide content
            newSlidePart.Slide = (Slide)blueprintSlidePart.Slide.CloneNode(true);
            
            // Copy Relationships correctly
            newSlidePart.AddPart(blueprintSlidePart.SlideLayoutPart!);
            
            // Copy other parts (images, backgrounds, etc)
            foreach (var part in blueprintSlidePart.Parts)
            {
                if (part.OpenXmlPart is SlideLayoutPart) continue;
                newSlidePart.AddPart(part.OpenXmlPart, part.RelationshipId);
            }

            return newSlidePart;
        }

        private void CleanPlaceholders(Slide slide)
        {
            var spTree = slide.CommonSlideData?.ShapeTree;
            if (spTree == null) return;

            var toRemove = spTree.Elements<OpenXmlElement>()
                .Where(e => e.InnerText.Contains("{{service.main}}") || e.InnerText.Contains("{{service.sub}}") || e.InnerText.Contains("{service.sub}"))
                .ToList();

            foreach (var element in toRemove)
            {
                element.Remove();
            }
        }

        private string CreateRowXml(ServiceRowData row, long y, string currency, Guid regionId)
        {
            string color = "0C5649"; // Dark Green
            bool useRect = row.Level == ServiceLevel.Main || row.Level == ServiceLevel.Sub;
            bool semiTransparent = row.Level == ServiceLevel.Sub;
            string alpha = semiTransparent ? "<a:alpha val=\"50199\"/>" : "";

            string rectXml = useRect ? $@"
<p:sp>
  <p:nvSpPr><p:cNvPr id=""10"" name=""rect""/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
  <p:spPr>
    <a:xfrm><a:off x=""232981"" y=""{y}""/><a:ext cx=""17822545"" cy=""420000""/></a:xfrm>
    <a:prstGeom prst=""rect""><a:avLst/></a:prstGeom>
    <a:solidFill><a:srgbClr val=""{color}"">{alpha}</a:srgbClr></a:solidFill>
    <a:ln><a:noFill/></a:ln>
  </p:spPr>
</p:sp>" : "";

            string textColor = (row.Level == ServiceLevel.Main) ? "FFFFFF" : "000000";
            
            decimal localPrice = 0;
            decimal usdPrice = 0;
            
            if (row.Selection != null)
            {
                localPrice = row.Selection.Service.Prices.FirstOrDefault(p => p.RegionId == regionId && p.Status == "Active")?.LocalPrice ?? 0;
                usdPrice = row.Selection.Service.Prices.FirstOrDefault(p => p.RegionId == regionId && p.Status == "Active")?.UsdReferencePrice ?? 0;
            }

            string VND = localPrice > 0 ? localPrice.ToString("N0") + (string.IsNullOrEmpty(row.Unit) ? "" : $" / {row.Unit}") : "To discuss";
            string USD = usdPrice > 0 ? usdPrice.ToString("N0") + (string.IsNullOrEmpty(row.Unit) ? "" : $" / {row.Unit}") : "To discuss";

            // If it's a structural parent or has children, usually prices are not shown at this level
            if (row.Selection == null || row.Level == ServiceLevel.Main || row.HasChildren)
            {
                VND = "";
                USD = "";
            }

            int fontSize = (row.Level == ServiceLevel.Range || row.Level == ServiceLevel.LineItem) ? 2000 : 1300;
            
            return $@"
<p:grpSp xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">
  <p:nvGrpSpPr><p:cNvPr id=""{(uint)Guid.NewGuid().GetHashCode()}"" name=""row""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
  <p:grpSpPr><a:xfrm><a:off x=""155325"" y=""{y}""/><a:ext cx=""11881697"" cy=""420000""/><a:chOff x=""232987"" y=""{y}""/><a:chExt cx=""17822545"" cy=""420000""/></a:xfrm></p:grpSpPr>
  {rectXml}
  {CreateTextboxXml(440670, y, 2973434, row.DisplayName, textColor, row.Level == ServiceLevel.Main, fontSize)}
  {CreateTextboxXml(3667872, y, 2491419, VND, textColor, false, fontSize)}
  {CreateTextboxXml(6159291, y, 2491419, USD, textColor, false, fontSize)}
  {CreateTextboxXml(8650710, y, 2973434, "", textColor, false, fontSize)}
</p:grpSp>";
        }

        private string CreateTextboxXml(long x, long y, long cx, string text, string color, bool bold, int sz = 1100)
        {
            return $@"
<p:sp>
  <p:nvSpPr><p:cNvPr id=""{(uint)Guid.NewGuid().GetHashCode()}"" name=""txt""/><p:cNvSpPr txBox=""1""/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x=""{x}"" y=""{y}""/><a:ext cx=""{cx}"" cy=""420000""/></a:xfrm><a:noFill/><a:ln><a:noFill/></a:ln></p:spPr>
  <p:txBody>
    <a:bodyPr wrap=""none"" rtlCol=""0""><a:spAutoFit/></a:bodyPr>
    <a:p>
      <a:pPr algn=""ctr""/>
      <a:r>
        <a:rPr sz=""{sz}"" b=""{(bold ? "1" : "0")}""><a:solidFill><a:srgbClr val=""{color}""/></a:solidFill><a:latin typeface=""Arial""/></a:rPr>
        <a:t>{SecurityElement.Escape(text)}</a:t>
      </a:r>
    </a:p>
  </p:txBody>
</p:sp>";
        }

        private OpenXmlElement? ExtractShapeWithPlaceholder(SlidePart part, string placeholder)
        {
            foreach (var element in part.Slide.CommonSlideData!.ShapeTree!.Elements<OpenXmlElement>())
            {
                if (element.InnerText.Contains(placeholder))
                {
                    return element.CloneNode(true);
                }
            }
            return null;
        }

        private long? GetElementHeight(OpenXmlElement element)
        {
            var ext = element.Descendants<DocumentFormat.OpenXml.Drawing.Extents>().FirstOrDefault();
            return ext?.Cy?.Value;
        }

        private void AppendBlueprintRow(ShapeTree spTree, OpenXmlElement blueprint, string placeholder, string text, long y)
        {
            var clone = blueprint.CloneNode(true);
            
            // 1. Replace Text Robustly
            RobustReplaceText(clone, placeholder, text);

            // 2. Adjust Y Position
            // We need to find all Offset tags and update their Y
            var offsets = clone.Descendants<DocumentFormat.OpenXml.Drawing.Offset>().ToList();
            if (offsets.Any())
            {
                long originalY = offsets.First().Y?.Value ?? 0;
                long delta = y - originalY;
                foreach (var off in offsets)
                {
                    if (off.Y != null) off.Y.Value += delta;
                }
            }

            spTree.Append(clone);
        }

        private void RobustReplaceText(OpenXmlElement element, string placeholder, string newText)
        {
            var textElements = element.Descendants<DocumentFormat.OpenXml.Drawing.Text>().ToList();
            
            // Check if any single element has it all
            foreach (var te in textElements)
            {
                if (te.Text.Contains(placeholder))
                {
                    te.Text = te.Text.Replace(placeholder, newText);
                    return;
                }
            }

            // Fallback: It's split across runs. 
            // Join all, replace, then put back into the first run and clear others
            string aggregate = string.Join("", textElements.Select(te => te.Text));
            if (aggregate.Contains(placeholder))
            {
                string replaced = aggregate.Replace(placeholder, newText);
                for (int i = 0; i < textElements.Count; i++)
                {
                    if (i == 0) textElements[i].Text = replaced;
                    else textElements[i].Text = "";
                }
            }
        }

        private string StripXmlDeclaration(string xml)
        {
            xml = xml.Trim();
            if (xml.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                int closeIndex = xml.IndexOf("?>");
                if (closeIndex != -1) return xml.Substring(closeIndex + 2).Trim();
            }
            return xml;
        }

        private void AppendRawXmlToShapeTree(ShapeTree spTree, string xml)
        {
            // The OpenXML constructor for specific types is very sensitive to the root tag
            if (xml.Trim().StartsWith("<p:pic", StringComparison.OrdinalIgnoreCase))
                spTree.Append(new DocumentFormat.OpenXml.Presentation.Picture(xml));
            else if (xml.Trim().StartsWith("<p:grpSp", StringComparison.OrdinalIgnoreCase))
                spTree.Append(new DocumentFormat.OpenXml.Presentation.GroupShape(xml));
            else
                spTree.Append(new DocumentFormat.OpenXml.Presentation.Shape(xml));
        }

        private uint GetNextSlideId(PresentationPart presentationPart)
        {
            var slideIdList = presentationPart.Presentation.SlideIdList;
            if (slideIdList == null || !slideIdList.Elements<SlideId>().Any()) return 256;
            return slideIdList.Elements<SlideId>().Max(s => s.Id!.Value) + 1;
        }

        private List<ServiceRowData> BuildServiceTree(List<ProposalServiceSelection> selections, List<Cherry.Core.Entities.Service> relevantServices)
        {
            var selectionDict = selections.ToDictionary(s => s.ServiceId);
            var nodes = relevantServices.Select(s => new ServiceRowData 
            { 
                Service = s,
                Selection = selectionDict.GetValueOrDefault(s.Id)
            }).ToList();
            
            var dict = nodes.ToDictionary(n => n.Service.Id);

            var tree = new List<ServiceRowData>();
            foreach (var node in nodes)
            {
                if (node.Service.ParentId.HasValue && dict.TryGetValue(node.Service.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    node.Parent = parent;
                }
                else
                {
                    tree.Add(node);
                }
            }

            foreach (var node in nodes)
            {
                node.Children = node.Children.OrderBy(c => c.Service.SortOrder).ToList();
            }
            return tree.OrderBy(n => n.Service.SortOrder).ToList();
        }

        private List<ServiceRowData> FlattenTree(List<ServiceRowData> tree, string prefix = "")
        {
            var result = new List<ServiceRowData>();
            int i = 1;
            foreach (var node in tree)
            {
                string name = node.Service.Name;
                if (node.Level == ServiceLevel.Main)
                {
                    node.DisplayName = $"{i}. {name}";
                    result.Add(node);
                    result.AddRange(FlattenTree(node.Children, $"{i}."));
                }
                else if (node.Level == ServiceLevel.Sub)
                {
                    node.DisplayName = $"{prefix}{i}. {name}";
                    result.Add(node);
                    result.AddRange(FlattenTree(node.Children, $"{prefix}{i}."));
                }
                else // Leaf (Range or LineItem)
                {
                    node.DisplayName = $"• {name}";
                    result.Add(node);
                    result.AddRange(FlattenTree(node.Children, ""));
                }
                i++;
            }
            return result;
        }

        private class ServiceRowData
        {
            public Cherry.Core.Entities.Service Service { get; set; } = null!;
            public ProposalServiceSelection? Selection { get; set; }
            public List<ServiceRowData> Children { get; set; } = new();
            public ServiceRowData? Parent { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public ServiceLevel? LevelOverride { get; set; }
            public ServiceLevel Level => LevelOverride ?? Service.Level;
            public decimal LocalPrice => Service.Prices.FirstOrDefault()?.LocalPrice ?? 0;
            public decimal UsdPrice => Service.Prices.FirstOrDefault()?.UsdReferencePrice ?? 0;
            public string? Unit => Service.Unit;
            public bool HasChildren => Children.Any();
        }

        public Task ConvertPptxToPdfAsync(Guid artifactId)
        {
            // Implementation for PDF conversion
            return Task.CompletedTask;
        }
        private void RenderTiptapToParagraph(DocumentFormat.OpenXml.Drawing.Paragraph paragraph, string json, DocumentFormat.OpenXml.Drawing.RunProperties? baseRunProps, SlidePart? slidePart = null, DocumentFormat.OpenXml.Presentation.Shape? parentShape = null)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetProperty("type").GetString() != "doc") return;

            if (root.TryGetProperty("content", out var contentList))
            {
                bool firstBlock = true;
                foreach (var block in contentList.EnumerateArray())
                {
                    if (!firstBlock)
                    {
                        paragraph.AppendChild(new DocumentFormat.OpenXml.Drawing.Break());
                    }
                    RenderTiptapNode(paragraph, block, baseRunProps, slidePart, parentShape);
                    firstBlock = false;
                }
            }
        }

        private void RenderTiptapNode(DocumentFormat.OpenXml.Drawing.Paragraph paragraph, JsonElement node, DocumentFormat.OpenXml.Drawing.RunProperties? baseRunProps, SlidePart? slidePart = null, DocumentFormat.OpenXml.Presentation.Shape? parentShape = null)
        {
            if (node.ValueKind != JsonValueKind.Object) return;
            var type = node.GetProperty("type").GetString();

            if (type == "paragraph" || type == "heading" || type == "listItem")
            {
                if (node.TryGetProperty("content", out var children))
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        RenderTiptapNode(paragraph, child, baseRunProps, slidePart, parentShape);
                    }
                }
            }
            else if (type == "text")
            {
                var text = node.GetProperty("text").GetString();
                var run = new DocumentFormat.OpenXml.Drawing.Run();
                
                // Rebuild properties from scratch to ensure correct DrawingML order
                // Inherit attributes like font size (sz) from base if available
                var runProps = new DocumentFormat.OpenXml.Drawing.RunProperties();
                if (baseRunProps != null)
                {
                    foreach (var attr in baseRunProps.GetAttributes())
                    {
                        runProps.SetAttribute(attr);
                    }
                    if (baseRunProps.FontSize != null) runProps.FontSize = baseRunProps.FontSize;
                }

                // Default Marks
                runProps.Bold = false;
                runProps.Italic = false;
                runProps.Underline = DocumentFormat.OpenXml.Drawing.TextUnderlineValues.None;

                string? hexColor = null;
                string? fontFamily = null;

                if (node.TryGetProperty("marks", out var marks))
                {
                    foreach (var mark in marks.EnumerateArray())
                    {
                        var markType = mark.GetProperty("type").GetString();
                        if (markType == "bold") runProps.Bold = true;
                        if (markType == "italic") runProps.Italic = true;
                        if (markType == "underline") runProps.Underline = DocumentFormat.OpenXml.Drawing.TextUnderlineValues.Single;
                        
                        if (markType == "link")
                        {
                            runProps.Underline = DocumentFormat.OpenXml.Drawing.TextUnderlineValues.Single;
                            if (hexColor == null) hexColor = "0563C1"; // Link Blue
                        }

                        if ((markType == "textStyle" || markType == "color") && mark.TryGetProperty("attrs", out var attrs))
                        {
                            if (attrs.TryGetProperty("color", out var colorProp))
                            {
                                var hex = colorProp.GetString()?.Replace("#", "");
                                if (!string.IsNullOrEmpty(hex))
                                {
                                    hexColor = hex;
                                }
                            }
                            if (attrs.TryGetProperty("fontFamily", out var fontProp))
                            {
                                fontFamily = fontProp.GetString();
                            }
                        }
                    }
                }

                // DrawingML Order: Fill choice must come before latin font
                var finalHex = hexColor?.ToUpper() ?? "000000";
                if (hexColor != null) _logger.LogInformation("Applying verified Tiptap color: {Hex}", finalHex);
                
                runProps.AppendChild(new DocumentFormat.OpenXml.Drawing.SolidFill(
                    new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = finalHex }
                ));

                if (!string.IsNullOrEmpty(fontFamily))
                {
                    runProps.AppendChild(new DocumentFormat.OpenXml.Drawing.LatinFont { Typeface = fontFamily });
                }

                run.RunProperties = runProps;
                run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text(text ?? ""));
                paragraph.AppendChild(run);
            }
            else if (type == "bulletList" || type == "orderedList" || type == "taskList")
            {
                if (node.TryGetProperty("content", out var items))
                {
                    int index = 1;
                    foreach (var item in items.EnumerateArray())
                    {
                        var run = new DocumentFormat.OpenXml.Drawing.Run();
                        if (baseRunProps != null)
                        {
                            var prefixProps = new DocumentFormat.OpenXml.Drawing.RunProperties();
                            foreach(var attr in baseRunProps.GetAttributes()) prefixProps.SetAttribute(attr);
                            if (baseRunProps.FontSize != null) prefixProps.FontSize = baseRunProps.FontSize;
                            
                            prefixProps.AppendChild(new DocumentFormat.OpenXml.Drawing.SolidFill(
                                new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = "000000" }
                            ));
                            run.RunProperties = prefixProps;
                        }
                        
                        string prefix = "\u2022 "; // Default bullet
                        if (type == "orderedList") prefix = $"{index}. ";
                        else if (type == "taskList")
                        {
                            bool @checked = false;
                            if (item.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("checked", out var checkedProp))
                            {
                                @checked = checkedProp.GetBoolean();
                            }
                            prefix = @checked ? "[\u2713] " : "[ ] "; // Checkmark or empty box
                        }

                        run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text(prefix)); 
                        paragraph.AppendChild(run);

                        if (item.TryGetProperty("content", out var itemContent))
                        {
                            foreach (var subNode in itemContent.EnumerateArray())
                            {
                                RenderTiptapNode(paragraph, subNode, baseRunProps, slidePart, parentShape);
                            }
                        }
                        
                        paragraph.AppendChild(new DocumentFormat.OpenXml.Drawing.Break());
                        index++;
                    }
                }
            }
            else if (type == "image")
            {
                // In-line image support is limited in PPTX text boxes. 
                // We'll render a placeholder text for now or just skip it if we can't easily insert a Slide Picture part here.
                var run = new DocumentFormat.OpenXml.Drawing.Run();
                if (baseRunProps != null) run.RunProperties = (DocumentFormat.OpenXml.Drawing.RunProperties)baseRunProps.CloneNode(true);
                run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text("[IMAGE]"));
                paragraph.AppendChild(run);
            }
            else if (type == "table")
            {
                if (slidePart != null && parentShape != null)
                {
                    try
                    {
                        CreateActualPptxTable(slidePart, parentShape, node, baseRunProps);
                        // Once the table is created at the placeholder location, 
                        // we can optionally clear the text in the placeholder or hide it.
                        // For now, let's add a note that the table was moved.
                        var run = new DocumentFormat.OpenXml.Drawing.Run();
                        if (baseRunProps != null) run.RunProperties = (DocumentFormat.OpenXml.Drawing.RunProperties)baseRunProps.CloneNode(true);
                        run.AppendChild(new DocumentFormat.OpenXml.Drawing.Text("")); // Keep it empty so it doesn't show "[TABLE DATA]"
                        paragraph.AppendChild(run);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create actual PPTX table, falling back to text grid");
                    }
                }

                // Fallback: Text-based rendering for tables
                paragraph.AppendChild(new DocumentFormat.OpenXml.Drawing.Break());
                
                if (node.TryGetProperty("content", out var rows))
                {
                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.TryGetProperty("content", out var cells))
                        {
                            bool firstCell = true;
                            foreach (var cell in cells.EnumerateArray())
                            {
                                if (!firstCell)
                                {
                                    var separator = new DocumentFormat.OpenXml.Drawing.Run();
                                    if (baseRunProps != null) separator.RunProperties = (DocumentFormat.OpenXml.Drawing.RunProperties)baseRunProps.CloneNode(true);
                                    separator.AppendChild(new DocumentFormat.OpenXml.Drawing.Text("\t"));
                                    paragraph.AppendChild(separator);
                                }

                                if (cell.TryGetProperty("content", out var cellContent))
                                {
                                    foreach (var sub in cellContent.EnumerateArray())
                                    {
                                        RenderTiptapNode(paragraph, sub, baseRunProps, slidePart, parentShape);
                                    }
                                }
                                firstCell = false;
                            }
                        }
                        paragraph.AppendChild(new DocumentFormat.OpenXml.Drawing.Break());
                    }
                }
            }
        }

        private void CreateActualPptxTable(SlidePart slidePart, DocumentFormat.OpenXml.Presentation.Shape parentShape, JsonElement tableNode, DocumentFormat.OpenXml.Drawing.RunProperties? baseRunProps)
        {
            var shapeTree = slidePart.Slide.CommonSlideData?.ShapeTree;
            if (shapeTree == null) return;

            // In Presentation, ShapeProperties uses Transform2D (a:xfrm)
            var transform = parentShape.ShapeProperties?.Transform2D;
            if (transform == null) return;

            var graphicFrame = new DocumentFormat.OpenXml.Presentation.GraphicFrame();
            graphicFrame.NonVisualGraphicFrameProperties = new DocumentFormat.OpenXml.Presentation.NonVisualGraphicFrameProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = (uint)new Random().Next(10000, 99999), Name = "Table " + Guid.NewGuid().ToString().Substring(0, 4) },
                new DocumentFormat.OpenXml.Presentation.NonVisualGraphicFrameDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties());

            graphicFrame.Transform = new DocumentFormat.OpenXml.Presentation.Transform 
            { 
                Offset = (DocumentFormat.OpenXml.Drawing.Offset)transform.Offset?.CloneNode(true) ?? new DocumentFormat.OpenXml.Drawing.Offset { X = 0, Y = 0 },
                Extents = (DocumentFormat.OpenXml.Drawing.Extents)transform.Extents?.CloneNode(true) ?? new DocumentFormat.OpenXml.Drawing.Extents { Cx = 0, Cy = 0 }
            };

            var table = new DocumentFormat.OpenXml.Drawing.Table();
            table.AppendChild(new DocumentFormat.OpenXml.Drawing.TableProperties { FirstRow = true, BandRow = true });

            var tableGrid = new DocumentFormat.OpenXml.Drawing.TableGrid();
            table.AppendChild(tableGrid);

            // 1. Process Rows and Count Max Columns
            int maxCols = 0;
            if (tableNode.TryGetProperty("content", out var rows))
            {
                foreach (var rowNode in rows.EnumerateArray())
                {
                    var tableRow = new DocumentFormat.OpenXml.Drawing.TableRow { Height = 370840L };
                    
                    if (rowNode.TryGetProperty("content", out var cells))
                    {
                        int currentCols = 0;
                        foreach (var cellNode in cells.EnumerateArray())
                        {
                            var tableCell = new DocumentFormat.OpenXml.Drawing.TableCell();
                            var txBody = new DocumentFormat.OpenXml.Drawing.TextBody(
                                new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                                new DocumentFormat.OpenXml.Drawing.ListStyle()
                            );
                            
                            var paragraph = new DocumentFormat.OpenXml.Drawing.Paragraph();
                            if (cellNode.TryGetProperty("content", out var cellContent))
                            {
                                foreach (var sub in cellContent.EnumerateArray())
                                {
                                    RenderTiptapNode(paragraph, sub, baseRunProps, slidePart, null);
                                }
                            }
                            
                            txBody.AppendChild(paragraph);
                            tableCell.AppendChild(txBody);
                            tableCell.AppendChild(new DocumentFormat.OpenXml.Drawing.TableCellProperties());
                            
                            tableRow.AppendChild(tableCell);
                            currentCols++;
                        }
                        if (currentCols > maxCols) maxCols = currentCols;
                    }
                    table.AppendChild(tableRow);
                }
            }

            // 2. Add Grid Columns based on maxCols
            long totalWidth = transform.Extents?.Cx ?? 9144000L; 
            long colWidth = maxCols > 0 ? totalWidth / maxCols : totalWidth;

            for (int i = 0; i < maxCols; i++)
            {
                tableGrid.AppendChild(new DocumentFormat.OpenXml.Drawing.GridColumn { Width = colWidth });
            }

            var graphic = new DocumentFormat.OpenXml.Drawing.Graphic(
                new DocumentFormat.OpenXml.Drawing.GraphicData(table) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }
            );
            
            graphicFrame.AppendChild(graphic);
            shapeTree.AppendChild(graphicFrame);
            
            _logger.LogInformation("Successfully inserted actual PPTX table with {Rows} rows and {Cols} columns", table.Elements<DocumentFormat.OpenXml.Drawing.TableRow>().Count(), maxCols);
        }
    }
}
