using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

/// <summary>
/// A pending or resolved invitation for an email address to join a tenant with a given role
/// (and, optionally, initial household access). Invitations are matched to a user by email when
/// that user first authenticates, so the invite UX is identical whether or not the invitee
/// already exists in the external identity provider.
/// </summary>
[Index(nameof(TenantId), nameof(Email))]
[Index(nameof(Email))]
public class Invitation : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public TenantRole Role { get; set; }

    // Optional initial household access granted when the invitation is claimed. A user can hold a
    // role in multiple households, so an invite may carry several grants (or none).
    public ICollection<InvitationHouseholdAccess> Households { get; set; } = new List<InvitationHouseholdAccess>();

    // Optional: a HouseholdMember the invitee is linked to (a "Self" link) when the invitation is
    // claimed. Stored as a plain reference — no FK/nav — since it is applied and re-validated at
    // grant time and the invitation may outlive the member.
    public Guid? LinkedMemberId { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid? InvitedByUserId { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
