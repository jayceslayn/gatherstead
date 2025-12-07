using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<Household> Households { get; set; } = new List<Household>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
