using System;

namespace Gatherstead.Db.Entities;

public class ChoreAssignment
{
    public Guid Id { get; set; }
    public Guid ChoreTaskId { get; set; }
    public ChoreTask? ChoreTask { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public bool Volunteered { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
