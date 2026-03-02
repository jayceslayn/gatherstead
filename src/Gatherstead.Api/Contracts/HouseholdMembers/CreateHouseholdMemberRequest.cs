using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.HouseholdMembers;

public class CreateHouseholdMemberRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    public bool IsAdult { get; init; }

    [StringLength(64)]
    public string? AgeBand { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? DietaryNotes { get; init; }

    public string[]? DietaryTags { get; init; }

    /// <summary>
    /// Optional User ID to link this household member to an authenticated user.
    /// Tenant Owner/Manager can set any valid UserId.
    /// Household Admin can set any valid UserId within their household.
    /// Regular members can only set their own UserId.
    /// </summary>
    public Guid? UserId { get; init; }
}
