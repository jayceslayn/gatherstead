using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(TemplateId), nameof(Completed))]
[Index(nameof(TenantId), nameof(TemplateId), nameof(Day), nameof(TimeSlot), IsUnique = true)]
public class TaskPlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid TemplateId { get; set; }

    [ForeignKey(nameof(TemplateId))]
    public TaskTemplate? Template { get; set; }

    public DateOnly Day { get; set; }
    public TaskTimeSlot? TimeSlot { get; set; }
    public bool Completed { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }

    public ICollection<TaskIntent> Intents { get; set; } = new List<TaskIntent>();
}
