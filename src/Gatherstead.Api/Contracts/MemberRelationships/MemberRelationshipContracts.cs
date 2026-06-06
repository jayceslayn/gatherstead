using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MemberRelationships;

public record MemberRelationshipDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    Guid RelatedMemberId,
    RelationshipType RelationshipType,
    string? Notes,
    AuditInfo? Audit);

public class MemberRelationshipResponse : BaseEntityResponse<MemberRelationshipDto> { }

public class CreateMemberRelationshipRequest
{
    [Required]
    public Guid RelatedMemberId { get; init; }

    [Required]
    public RelationshipType RelationshipType { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public class UpdateMemberRelationshipRequest
{
    [Required]
    public RelationshipType RelationshipType { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}
