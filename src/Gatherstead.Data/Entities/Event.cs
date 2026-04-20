using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(PropertyId), nameof(Name), IsUnique = true)]
[Index(nameof(TenantId), nameof(StartDate))]
[Index(nameof(TenantId), nameof(EndDate))]
public class Event : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ICollection<MealTemplate> MealTemplates { get; set; } = new List<MealTemplate>();
    public ICollection<ChoreTemplate> ChoreTemplates { get; set; } = new List<ChoreTemplate>();
    public ICollection<EventAttendance> Attendances { get; set; } = new List<EventAttendance>();
}
