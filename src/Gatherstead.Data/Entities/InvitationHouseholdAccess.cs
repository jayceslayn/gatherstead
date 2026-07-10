using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

/// <summary>
/// A single household-access grant attached to an <see cref="Invitation"/>. An invitation may carry
/// several of these, mirroring how a user can hold a per-household role in multiple households; they
/// are applied when the invitation is claimed.
/// </summary>
[PrimaryKey(nameof(InvitationId), nameof(HouseholdId))]
[Index(nameof(TenantId))]
[Index(nameof(HouseholdId))]
public class InvitationHouseholdAccess : AuditableEntity
{
    public Guid InvitationId { get; set; }
    [ForeignKey(nameof(InvitationId))]
    public Invitation? Invitation { get; set; }

    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdId { get; set; }
    [ForeignKey(nameof(HouseholdId))]
    public Household? Household { get; set; }

    public HouseholdRole Role { get; set; }
}
