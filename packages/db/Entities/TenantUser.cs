using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[PrimaryKey(nameof(TenantId), nameof(UserId))]
[Index(nameof(TenantId))]
[Index(nameof(UserId))]
public class TenantUser : AuditableEntity
{
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public TenantRole Role { get; set; }
}
