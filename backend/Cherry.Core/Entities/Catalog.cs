using System;

namespace Cherry.Core.Entities
{
    public class Region
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty; // e.g. "VN", "TH"
        public string Name { get; set; } = string.Empty;
        public string LocalCurrency { get; set; } = string.Empty; // e.g. "VND", "THB"
        public bool IsActive { get; set; } = true;
    }

    public enum ServiceLevel
    {
        Main = 1,
        Sub = 2,
        Range = 3
    }

    public class Service
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ParentId { get; set; }
        public Service? Parent { get; set; }
        public ServiceLevel Level { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<Service> Children { get; set; } = new List<Service>();
        public ICollection<ServicePrice> Prices { get; set; } = new List<ServicePrice>();
    }

    public class ServicePrice
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;
        public Guid RegionId { get; set; }
        public Region Region { get; set; } = null!;
        public decimal LocalPrice { get; set; }
        public decimal UsdReferencePrice { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class ExchangeRateRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BaseCurrency { get; set; } = "USD";
        public string QuoteCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Source { get; set; } = "System";
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsDefault { get; set; }
    }
}
