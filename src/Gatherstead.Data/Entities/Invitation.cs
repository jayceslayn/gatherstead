using System;
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

    // Optional initial household access granted when the invitation is claimed.
    public Guid? HouseholdId { get; set; }
    [ForeignKey(nameof(HouseholdId))]
    public Household? Household { get; set; }

    public HouseholdRole? HouseholdRole { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid? InvitedByUserId { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
