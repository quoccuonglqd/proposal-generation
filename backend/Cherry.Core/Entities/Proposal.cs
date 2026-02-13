using System;
using System.Collections.Generic;

namespace Cherry.Core.Entities
{
    public enum ProposalStatus
    {
        Draft,
        Generating,
        Generated,
        Finalized,
        Approved,
        Error
    }

    public class Proposal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ClientName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public Guid RegionId { get; set; }
        public Region Region { get; set; } = null!;
        public ProposalStatus Status { get; set; } = ProposalStatus.Draft;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string DefaultLanguage { get; set; } = "en";
        public Guid? TemplateId { get; set; }
        public Template? Template { get; set; }

        public ICollection<ProposalSection> Sections { get; set; } = new List<ProposalSection>();
        public ICollection<ProposalServiceSelection> ServiceSelections { get; set; } = new List<ProposalServiceSelection>();
        public ICollection<ProposalVersion> Versions { get; set; } = new List<ProposalVersion>();
    }

    public class ProposalSection
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProposalId { get; set; }
        public Proposal Proposal { get; set; } = null!;
        public string SectionKey { get; set; } = string.Empty;
        public int Order { get; set; }
        public string BackgroundType { get; set; } = "None"; // None, Color, Image
        public string? BackgroundAssetId { get; set; }
        public string ContentJson { get; set; } = "{}"; // Stores per-language content
    }

    public class ProposalServiceSelection
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProposalId { get; set; }
        public Proposal Proposal { get; set; } = null!;
        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string? ChosenUnit { get; set; }
        public string? Notes { get; set; }
    }

    public class ProposalVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProposalId { get; set; }
        public Proposal Proposal { get; set; } = null!;
        public int VersionNo { get; set; }
        public decimal ExchangeRateUsed { get; set; }
        public string PricingSnapshotJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;

        public ICollection<ProposalArtifact> Artifacts { get; set; } = new List<ProposalArtifact>();
    }

    public enum ArtifactType
    {
        Pptx,
        Pdf
    }

    public class ProposalArtifact
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProposalVersionId { get; set; }
        public ProposalVersion ProposalVersion { get; set; } = null!;
        public ArtifactType Type { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string? Checksum { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
