using System;

namespace Cherry.Core.Entities
{
    public enum TemplateStatus
    {
        Draft,
        Active,
        Inactive
    }

    public class Template
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
        public string? Description { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
        public string Category { get; set; } = "Standard";
        public Guid? RegionId { get; set; }
        public Region? Region { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
