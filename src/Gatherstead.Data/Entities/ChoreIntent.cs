using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(ChorePlanId), nameof(HouseholdMemberId), IsUnique = true)]
public class ChoreIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid ChorePlanId { get; set; }

    [ForeignKey(nameof(ChorePlanId))]
    public ChorePlan? ChorePlan { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public bool Volunteered { get; set; }
}
