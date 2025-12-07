using System;
using System.Collections.Generic;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class ChoreTask : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public ChoreTemplate? Template { get; set; }
    public DateOnly Day { get; set; }
    public MealType? MealType { get; set; }
    public bool Completed { get; set; }
    public string? Notes { get; set; }

    public ICollection<ChoreAssignment> Assignments { get; set; } = new List<ChoreAssignment>();
}
