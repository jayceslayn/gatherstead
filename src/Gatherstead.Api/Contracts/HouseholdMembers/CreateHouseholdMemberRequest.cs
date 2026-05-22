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
}
