using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.AccommodationIntents;

public record AccommodationIntentDto(
    Guid Id,
    Guid TenantId,
    Guid AccommodationId,
    Guid HouseholdMemberId,
    DateOnly Night,
    AccommodationIntentStatus Status,
    string? Notes,
    AccommodationIntentDecision Decision,
    int? PartySize,
    int? Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class AccommodationIntentResponse : BaseEntityResponse<AccommodationIntentDto> { }

public class CreateAccommodationIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public DateOnly Night { get; init; }

    [Required]
    public AccommodationIntentStatus Status { get; init; }

    public string? Notes { get; init; }
    public int? PartySize { get; init; }
    public int? Priority { get; init; }
}

public class UpdateAccommodationIntentRequest
{
    [Required]
    public AccommodationIntentStatus Status { get; init; }

    public string? Notes { get; init; }
    public AccommodationIntentDecision Decision { get; init; }
    public int? PartySize { get; init; }
    public int? Priority { get; init; }
}
