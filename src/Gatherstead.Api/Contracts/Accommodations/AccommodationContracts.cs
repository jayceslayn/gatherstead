using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Accommodations;

public record AccommodationDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string Name,
    AccommodationType Type,
    int? CapacityAdults,
    int? CapacityChildren,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class AccommodationResponse : BaseEntityResponse<AccommodationDto> { }

public class CreateAccommodationRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AccommodationType Type { get; init; }

    public int? CapacityAdults { get; init; }
    public int? CapacityChildren { get; init; }
    public string? Notes { get; init; }
}

public class UpdateAccommodationRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AccommodationType Type { get; init; }

    public int? CapacityAdults { get; init; }
    public int? CapacityChildren { get; init; }
    public string? Notes { get; init; }
}
