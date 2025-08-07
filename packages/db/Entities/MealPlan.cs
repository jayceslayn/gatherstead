using System;
using System.Collections.Generic;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class MealPlan
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public DateOnly Day { get; set; }
    public MealType MealType { get; set; }
    public string? Notes { get; set; }

    public ICollection<MealIntent> Intents { get; set; } = new List<MealIntent>();
}
