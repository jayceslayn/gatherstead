using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(ChoreTaskId), nameof(HouseholdMemberId), IsUnique = true)]
public class ChoreAssignment : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid ChoreTaskId { get; set; }
    [ForeignKey(nameof(ChoreTaskId))]
    public ChoreTask? ChoreTask { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    public bool Volunteered { get; set; }
}
