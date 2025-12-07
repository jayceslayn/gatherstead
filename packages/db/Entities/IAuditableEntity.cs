using System;

namespace Gatherstead.Db.Entities;

public interface IAuditableEntity
{
    Guid CreatedByUserId { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    Guid? UpdatedByUserId { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    bool IsDeleted { get; set; }
    Guid? DeletedByUserId { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
