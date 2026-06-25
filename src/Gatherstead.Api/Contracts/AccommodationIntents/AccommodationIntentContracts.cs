using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.AccommodationIntents;

public record AccommodationIntentDto(
    Guid Id,
    Guid TenantId,
    Guid AccommodationId,
    Guid HouseholdMemberId,
    DateOnly StartNight,
    DateOnly EndNight,
    AccommodationIntentStatus Status,
    string? Notes,
    AccommodationIntentDecision Decision,
    int? PartyAdults,
    int? PartyChildren,
    int? Priority,
    AuditInfo? Audit);

public class AccommodationIntentResponse : BaseEntityResponse<AccommodationIntentDto> { }

/// <summary>A member's stay enriched with its accommodation and property names, for cross-accommodation
/// "my stays" listings (the top-level Accommodations feature and dashboard widget).</summary>
public record MyStayDto(
    Guid Id,
    Guid AccommodationId,
    string AccommodationName,
    Guid PropertyId,
    string PropertyName,
    Guid HouseholdMemberId,
    DateOnly StartNight,
    DateOnly EndNight,
    AccommodationIntentStatus Status,
    AccommodationIntentDecision Decision,
    int? PartyAdults,
    int? PartyChildren);

public class CreateAccommodationIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public DateOnly StartNight { get; init; }

    [Required]
    public DateOnly EndNight { get; init; }

    [Required]
    public AccommodationIntentStatus Status { get; init; }

    public string? Notes { get; init; }
    public int? PartyAdults { get; init; }
    public int? PartyChildren { get; init; }
    public int? Priority { get; init; }
}

public class UpdateAccommodationIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    /// <summary>Desired (target) accommodation; differs from the route's accommodationId to move the stay.</summary>
    [Required]
    public Guid AccommodationId { get; init; }

    [Required]
    public DateOnly StartNight { get; init; }

    [Required]
    public DateOnly EndNight { get; init; }

    [Required]
    public AccommodationIntentStatus Status { get; init; }

    public string? Notes { get; init; }
    public AccommodationIntentDecision Decision { get; init; }
    public int? PartyAdults { get; init; }
    public int? PartyChildren { get; init; }
    public int? Priority { get; init; }
}
