using System;

namespace Gatherstead.Db.Entities;

public class ChoreAssignment : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ChoreTaskId { get; set; }
    public ChoreTask? ChoreTask { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public bool Volunteered { get; set; }
}
