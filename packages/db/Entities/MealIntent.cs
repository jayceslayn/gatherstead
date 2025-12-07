using System;
using System.ComponentModel.DataAnnotations.Schema;
using Gatherstead.Db.Encryption;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(MealPlanId))]
[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(MealPlanId), nameof(HouseholdMemberId), IsUnique = true)]
public class MealIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid MealPlanId { get; set; }
    [ForeignKey(nameof(MealPlanId))]
    public MealPlan? MealPlan { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    public MealIntentStatus Status { get; set; }
    public bool BringOwnFood { get; set; }
    public string? Notes { get; set; }
}
