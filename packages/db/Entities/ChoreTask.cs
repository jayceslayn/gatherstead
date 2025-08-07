using System;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class ChoreTask
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public ChoreTemplate? Template { get; set; }
    public DateOnly Day { get; set; }
    public MealType? MealType { get; set; }
    public Guid[] AssigneeIds { get; set; } = Array.Empty<Guid>();
    public bool Completed { get; set; }
    public string? Notes { get; set; }
}
