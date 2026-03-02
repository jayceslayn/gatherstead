using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.HouseholdMembers;

public class UpdateHouseholdMemberRequest
{
    private string _name = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name
    {
        get => _name;
        init => _name = (value ?? string.Empty).Trim();
    }

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
    /// Regular members can only set their own UserId on an unlinked member.
    /// </summary>
    public Guid? UserId { get; init; }
}
