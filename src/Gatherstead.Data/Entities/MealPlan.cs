using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(MealTemplateId))]
[Index(nameof(TenantId), nameof(MealTemplateId), nameof(Day), nameof(MealType), IsUnique = true)]
public class MealPlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid MealTemplateId { get; set; }

    [ForeignKey(nameof(MealTemplateId))]
    public MealTemplate? MealTemplate { get; set; }

    public DateOnly Day { get; set; }
    public MealType MealType { get; set; }
    public string? Notes { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }

    public ICollection<MealIntent> Intents { get; set; } = new List<MealIntent>();
}
