using System;
using System.Collections.Generic;

namespace Cherry.Core.Dtos
{
    public record RegionDto(Guid Id, string Code, string Name, string LocalCurrency);

    public record ServiceTreeDto(
        Guid Id, 
        string Name, 
        string Level, 
        PriceDto? Price, 
        List<ServiceTreeDto> Children
    );

    public record PriceDto(decimal Local, decimal UsdRef);

    public record UpsertServiceRequest(
        Guid? ParentId, 
        string Level, 
        string Name, 
        int SortOrder, 
        bool IsActive = true
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
