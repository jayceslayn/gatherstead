using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<Household> Households { get; set; } = new List<Household>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
