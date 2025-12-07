using System;
using System.Collections.Generic;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class ChoreTemplate : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChoreTimeSlot TimeSlot { get; set; }
    public int? MinimumAssignees { get; set; }
    public string? Notes { get; set; }

    public ICollection<ChoreTask> Tasks { get; set; } = new List<ChoreTask>();
}
