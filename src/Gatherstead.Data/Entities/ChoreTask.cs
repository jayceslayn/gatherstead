using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(TemplateId))]
[Index(nameof(TenantId), nameof(TemplateId), nameof(Day), nameof(MealType), IsUnique = true)]
public class ChoreTask : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid TemplateId { get; set; }

    [ForeignKey(nameof(TemplateId))]
    public ChoreTemplate? Template { get; set; }

    public DateOnly Day { get; set; }
    public MealType? MealType { get; set; }
    public bool Completed { get; set; }
    public string? Notes { get; set; }

    public ICollection<ChoreAssignment> Assignments { get; set; } = new List<ChoreAssignment>();
}
