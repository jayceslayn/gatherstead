using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class Event
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
    public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
    public ICollection<ChoreTemplate> ChoreTemplates { get; set; } = new List<ChoreTemplate>();
}
