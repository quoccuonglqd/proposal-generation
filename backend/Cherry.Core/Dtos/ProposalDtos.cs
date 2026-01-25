using System;
using System.Collections.Generic;

namespace Cherry.Core.Dtos
{
    public record CreateProposalRequest(string ClientName, Guid RegionId, Guid? TemplateId, string LanguageDefault = "en");

    public record ProposalSummaryDto(Guid Id, string ClientName, string RegionName, string Status, DateTime UpdatedAt);

    public record ProposalDetailDto(
        Guid Id, 
        string ClientName, 
        Guid RegionId, 
        string Status, 
        string Language,
        Guid? TemplateId,
        List<ProposalSectionDto> Sections,
        List<ProposalServiceSelectionDto> ServiceSelections,
        List<ProposalArtifactDto> Artifacts
    );

    public record ProposalArtifactDto(Guid Id, string Type, string DownloadUrl, string FileName);

    public record ProposalSectionDto(string Key, int Order, string BackgroundType, string? BackgroundAssetId, object Content);

    public record ProposalServiceSelectionDto(Guid ServiceId, string ServiceName, decimal Quantity, string? Notes, PriceDto Price);

    public record UpdateProposalMetadataRequest(string ClientName, Guid RegionId);

    public record BatchDeleteRequest(List<Guid> ProposalIds);

    public record UpdateProposalServicesRequest(List<ServiceSelectionItem> Items);
    public record ServiceSelectionItem(Guid ServiceId, decimal Quantity, string? Notes);

    public record UpdateSectionsOrderRequest(List<string> OrderedKeys);
    public record UpdateSectionContentRequest(SectionBackgroundDto Background, Dictionary<string, object> Content);
    public record SectionBackgroundDto(string Type, string? AssetId);

    public record GenerateProposalRequest(string Language, ExchangeRateOverride? ExchangeRateOverride);
    public record ExchangeRateOverride(string Base, string Quote, decimal Rate);
}
