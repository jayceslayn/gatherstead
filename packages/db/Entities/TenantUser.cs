using System;

namespace Gatherstead.Db.Entities;

public class TenantUser
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public TenantRole Role { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
