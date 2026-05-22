using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[PrimaryKey(nameof(HouseholdId), nameof(UserId))]
[Index(nameof(TenantId))]
[Index(nameof(HouseholdId))]
[Index(nameof(UserId))]
public class HouseholdUser : AuditableEntity
{
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdId { get; set; }
    [ForeignKey(nameof(HouseholdId))]
    public Household? Household { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public HouseholdRole Role { get; set; }
}
