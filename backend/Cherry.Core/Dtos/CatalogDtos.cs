using System;
using System.Collections.Generic;

namespace Cherry.Core.Dtos
{
    public record RegionDto(Guid Id, string Code, string Name, string LocalCurrency);

    public record ServiceTreeDto(
        Guid Id, 
        Guid? ParentId,
        string Name, 
        string Level, 
        string? Unit,
        PriceDto? Price, 
        List<ServiceTreeDto> Children
    );

    public record PriceDto(decimal Local, decimal UsdRef);

    public record UpsertServiceRequest(
        Guid? ParentId, 
        string Level, 
        string Name, 
        string? Unit,
        int SortOrder, 
        bool IsActive = true,
        Guid? RegionId = null,
        decimal? LocalPrice = null,
        decimal? UsdReferencePrice = null
    );

    public record UpsertPriceRequest(
        Guid ServiceId, 
        Guid RegionId, 
        decimal LocalPrice, 
        decimal UsdReferencePrice, 
        DateTime EffectiveFrom
    );

    public record MasterSectionDto(
        Guid Id,
        string SectionKey,
        string Name,
        object DefaultContent,
        int SortOrder,
        bool IsActive
    );

    public record UpsertMasterSectionRequest(
        string SectionKey,
        string Name,
        object DefaultContent,
        int SortOrder,
        bool IsActive = true
    );
}
