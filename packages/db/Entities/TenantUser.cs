using System;

namespace Gatherstead.Db.Entities;

public class TenantUser : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public TenantRole Role { get; set; }
}
