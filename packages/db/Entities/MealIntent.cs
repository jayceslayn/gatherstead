using System;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class MealIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public MealPlan? MealPlan { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public MealIntentStatus Status { get; set; }
    public bool BringOwnFood { get; set; }
    public string? Notes { get; set; }
}
