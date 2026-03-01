using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MemberRelationships;

public class CreateMemberRelationshipRequest
{
    [Required]
    public Guid RelatedMemberId { get; init; }

    [Required]
    public RelationshipType RelationshipType { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}
