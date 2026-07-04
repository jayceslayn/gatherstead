using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Accommodations;

public record AccommodationDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string Name,
    AccommodationType Type,
    decimal? WidthMeters,
    decimal? DepthMeters,
    decimal? AreaSqMeters,
    /// <summary>Area override when set, otherwise width × depth. Null when neither is known.</summary>
    decimal? EffectiveAreaSqMeters,
    string? Notes,
    IReadOnlyList<BedDto> Beds,
    IReadOnlyList<AttributeDto> Attributes,
    AuditInfo? Audit);

/// <summary>One line of bed inventory: a quantity of a given size.</summary>
public record BedDto(Guid Id, BedSize Size, int Quantity);

/// <summary>Write shape for a bed inventory line. One entry per <see cref="Size"/>; quantities collapse per size.</summary>
public record BedWriteEntry(BedSize Size, int Quantity);

public class AccommodationResponse : BaseEntityResponse<AccommodationDto> { }

public class CreateAccommodationRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AccommodationType Type { get; init; }

    public decimal? WidthMeters { get; init; }
    public decimal? DepthMeters { get; init; }
    public decimal? AreaSqMeters { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<BedWriteEntry>? Beds { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

public class UpdateAccommodationRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AccommodationType Type { get; init; }

    public decimal? WidthMeters { get; init; }
    public decimal? DepthMeters { get; init; }
    public decimal? AreaSqMeters { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<BedWriteEntry>? Beds { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
