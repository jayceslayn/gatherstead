using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.HouseholdMembers;

public record HouseholdMemberDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdId,
    string Name,
    bool IsAdult,
    string? AgeBand,
    DateOnly? BirthDate,
    string? DietaryNotes,
    string[] DietaryTags,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    IReadOnlyList<AttributeDto> Attributes);

public class HouseholdMemberResponse : BaseEntityResponse<HouseholdMemberDto> { }

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

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

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

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
