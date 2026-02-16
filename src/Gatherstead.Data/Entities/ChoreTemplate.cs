using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(EventId))]
[Index(nameof(TenantId), nameof(EventId), nameof(Name), nameof(TimeSlot), IsUnique = true)]
public class ChoreTemplate : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ChoreTimeSlot TimeSlot { get; set; }
    public int? MinimumAssignees { get; set; }
    public string? Notes { get; set; }

    public ICollection<ChoreTask> Tasks { get; set; } = new List<ChoreTask>();
}
